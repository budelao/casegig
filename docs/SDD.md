# Software Design Document (SDD)
## CaseGig — API + Worker para Ordens de Investimento (Aporte/Resgate)

---

# 1. Visão Geral

Este documento descreve a solução atual do CaseGig para criação e processamento de ordens de investimento em fundos.

O sistema oferece:
- Criação de ordens imediatas (APORTE/RESGATE) respeitando regras do fundo e do cliente
- Criação de ordens agendadas (data futura e dia útil)
- Processamento assíncrono das ordens agendadas via Worker (processo separado)
- Persistência transacional com EF Core (MySQL) e controle de concorrência (RowVersion)
- Idempotência nos endpoints `POST` para suportar retries sem duplicar ordens
- Logs estruturados com export opcional (Splunk/Loki/Datadog) conforme configuração

---

# 2. Arquitetura da Solução

## 2.1 Estrutura (Clean Architecture)

Projetos:
- `CaseGig.Api`: entrada HTTP (Controllers/Middlewares) e composição do host
- `CaseGig.Worker`: processamento em background (HostedService) e composição do host
- `CaseGig.Application`: casos de uso, DTOs e contratos (abstrações)
- `CaseGig.Domain`: entidades, enums e regras de negócio (DDD rico)
- `CaseGig.Infrastructure`: persistência (EF Core), repositórios, transações e componentes técnicos (ex.: export de observabilidade)

Regras de dependência:
- Domain não depende de nenhuma camada
- Application depende apenas de Domain
- Infrastructure depende de Domain e de interfaces da Application
- API/Worker dependem de Application + Infrastructure (somente composição e orquestração)

## 2.2 Decisão de modelagem (DDD rico)

O modelo de domínio é behavior-driven:
- Entidades possuem métodos que aplicam invariantes e regras
- A Application coordena fluxo, transação, repositórios e concerns técnicos (ex.: idempotência)
- Regras de negócio não ficam espalhadas em handlers procedurais

---

# 3. Modelagem de Domínio

## 3.1 Enums

- `TipoOperacao`: `APORTE`, `RESGATE`
- `StatusCaptacao`: `ABERTO`, `FECHADO`
- `StatusOrdem`: `CRIADA`, `AGENDADA`, `EM_PROCESSAMENTO`, `CONCLUIDA`, `CANCELADA`, `REJEITADA`

## 3.2 Entidades e responsabilidades

### Cliente
Principais dados:
- `IdCliente`, `Cpf`, `Nome`, `SaldoDisponivel`, `RowVersion`

Comportamentos:
- Criação de ordens imediatas e agendadas:
  - valida fundo aberto e cut-off (quando imediato)
  - valida saldo/cotas (conforme tipo)
  - valida permanência mínima após resgate

### Fundo
Principais dados:
- `IdFundo`, `Nome`, `HorarioCorte`, `ValorCota`, `ValorMinimoAporte`, `ValorMinimoPermanencia`, `StatusCaptacao`, `RowVersion`

Comportamentos:
- Validar se está aberto para operações
- Validar se operação imediata está dentro do cut-off
- Validar data de agendamento (futura e dia útil)
- Calcular data/hora de execução a partir de `DateOnly` + `HorarioCorte`

Observação do case:
- `ValorMinimoAporte` e `ValorMinimoPermanencia` são aplicados sobre a quantidade de cotas (regra do case), mesmo que os nomes sugiram “valor monetário”.

### Posicao
Principais dados:
- Chave composta: (`IdCliente`, `IdFundo`)
- `QuantidadeCotas`, `RowVersion`

Comportamentos:
- `CreditarCotas(...)`
- `DebitarCotas(...)`

### Ordem
Principais dados:
- `IdOrdem`, `IdCliente`, `IdFundo`, `TipoOperacao`, `QuantidadeCotas`
- `DataCriacao`, `DataAgendamento?`, `DataProcessamento?`
- `Status`, `RowVersion`

Comportamentos:
- `Agendar(...)`: define `Status=AGENDADA` e `DataAgendamento`
- `PrepararParaProcessamento(...)`: valida status e elegibilidade por data
- `Processar(...)`: executa APORTE/RESGATE aplicando regras e altera saldo/posição
- `Concluir(...)` / `Rejeitar(...)`: transições finais e timestamp

---

# 4. Fluxos Principais

## 4.1 Criar Ordem (imediata)

1) API recebe requisição `POST /ordens`
2) UseCase carrega cliente/fundo/posição e coordena transação
3) Domínio cria a ordem (`Cliente.CriarOrdemImediata`) e processa (`Ordem.Processar`)
4) Persistência grava alterações (ordem, saldo e posição)
5) Retorna `201` (ou `200` em replay de idempotência)

Regras aplicadas:
- Fundo deve estar `ABERTO`
- Operação imediata deve estar dentro do cut-off do fundo
- APORTE: saldo suficiente e quantidade mínima de cotas
- RESGATE: cotas suficientes e permanência mínima (cotas restantes)

## 4.2 Criar Ordem (agendada)

1) API recebe `POST /ordens/agendamento` com `dataAgendamento` (date-only)
2) UseCase valida idempotência, coordena transação e chama domínio
3) Domínio valida data futura e dia útil; cria ordem e agenda com `HorarioCorte`
4) Persistência grava a ordem com `Status=AGENDADA` e `DataAgendamento` (datetime)
5) Retorna `201` (ou `200` em replay de idempotência)

## 4.3 Processar Ordens Agendadas (Worker)

1) Worker executa periodicamente (intervalo configurável)
2) Busca ordens elegíveis:
- `Status=AGENDADA`
- `DataAgendamento` dentro do dia de referência do worker (00:00 <= DataAgendamento < 00:00 do dia seguinte)
3) Para cada ordem:
- chama `Ordem.Processar(...)` com cliente/fundo/posição
- em violação de regra: `Ordem.Rejeitar(...)`
- em concorrência: registra conflito e segue

---

# 5. Persistência, Transações e Concorrência

## 5.1 Banco e ORM

- Banco relacional (MySQL) com EF Core
- Tabelas principais: `Clientes`, `Fundos`, `Posicoes`, `Ordens`

## 5.2 Transações

- A Application coordena transações via abstração (`ITransactionManager`)
- Alterações de ordem/saldo/posição são persistidas de forma consistente

## 5.3 Concorrência (otimista)

- Entidades possuem `RowVersion` como token de concorrência
- Conflitos são tratados como condição normal em cenários concorrentes

## 5.4 Idempotência (concern técnico)

- Endpoints `POST` aceitam `Idempotency-Key`
- A Application calcula hash do payload e aplica semântica:
  - mesma key + mesmo payload → replay (sem duplicar ordem)
  - mesma key + payload diferente → `409 Conflict`

Persistência:
- Metadados de idempotência são gravados em `Ordens` via shadow properties no EF Core
- Índice único: (`IdCliente`, `IdempotencyOperation`, `IdempotencyKey`)

---

# 6. Observabilidade (Logs Estruturados + Export)

## 6.1 Logs locais

- Saída em console (API: pretty em Development e JSON em não-Development; Worker: pretty para leitura local)
- API inclui middleware de logging de request (CorrelationId e métricas de tempo)

## 6.2 Export (Splunk / Grafana Loki / Datadog)

- API e Worker podem exportar logs via HTTP quando habilitado
- O export é feito em background para não bloquear request/worker

Configuração:
- `Observability:Logging:Enabled` (master switch)
- `Observability:Logging:Export:{Target}:Enabled` + credenciais/endpoint por destino

Resiliência:
- HttpClients com Polly (timeout, retry com backoff+jitter, circuit breaker)

---

# 7. Datas, Cultura e Timezone

- Entrada/saída humana usa pt-BR (`dd/MM/yyyy`) na borda (API)
- O domínio trabalha com `DateOnly` (agendamento) e `DateTime` (execução)
- O banco persiste datas como `datetime`
- O Worker usa “dia de referência” derivado do relógio do processo (hoje)

Observação:
- Em produção, a política de timezone deve ser explicitada (ex.: `America/Sao_Paulo` ou UTC + conversão na borda).

---

# 8. Trade-offs

- Não uso de mensageria no escopo do case (SQS/Kafka):
  - reduz complexidade e facilita execução local
  - limita escala e desacoplamento imediato
- Uso de Worker ao invés de arquitetura distribuída:
  - simples e suficiente para o case
  - roadmap previsto para evolução event-driven

---

# 9. Evolução para AWS (Lambda + SQS + EventBridge)

Evolução sugerida, sem reescrever regras de negócio:
1) Introduzir interface de publicação de eventos na Application (ex.: `IEventBus`)
2) Publicar eventos de domínio/aplicação (ex.: `OrdemAgendadaCriada`, `OrdemCriada`)
3) EventBridge roteia eventos por tipo
4) SQS como buffer (com DLQ)
5) Lambda consumer processa mensagens e chama a mesma camada Application/Domain
6) Reforçar idempotência no consumo (at-least-once controlado)

---

# 10. Conclusão

A solução equilibra simplicidade e robustez:
- Regras de negócio concentradas no domínio (DDD rico)
- Camadas com responsabilidades claras (Clean Architecture)
- Processamento assíncrono via Worker e evolução planejada para event-driven
- Idempotência, transações, concorrência e observabilidade tratados como concerns técnicos, com implementação consistente para API e Worker
