# Software Design Document (SDD)
## Sistema de Gestão de Ordens de Investimento

---

# 1. Visão Geral

Este documento descreve a solução proposta para o gerenciamento de ordens de aplicação e resgate em fundos de investimento.

O sistema permite:
- Criação de ordens de aplicação e resgate
- Validação de saldo e posição do cliente
- Controle de horários de cut-off por fundo
- Agendamento de ordens fora do horário permitido
- Processamento posterior de ordens agendadas

O domínio exige alto nível de consistência, controle de concorrência e integridade financeira.

---

# 2. Arquitetura da Solução

A solução foi construída utilizando arquitetura em camadas, com separação clara de responsabilidades:

- API: exposição de endpoints REST
- Application: orquestração de casos de uso
- Domain: regras de negócio e entidades
- Infrastructure: persistência e integração com banco de dados

## Decisões principais

- Uso de .NET 8
- API REST
- Banco de dados relacional
- EF Core como ORM
- Background Worker (HostedService) para processamento assíncrono de ordens agendadas

## Justificativa

Optou-se por uma arquitetura simples, porém robusta, visando:
- Facilidade de entendimento e execução do case
- Clareza na separação de responsabilidades
- Possibilidade de evolução futura

---

# 3. Modelagem de Domínio

## Entidades principais

### Cliente
- Id
- Nome
- Saldo

### Fundo
- Id
- Nome
- HorarioCutoff

### Ordem
- Id
- ClienteId
- FundoId
- Tipo (Aplicacao | Resgate)
- Valor
- Status (Criada | Agendada | Processada | Rejeitada)
- DataCriacao
- DataExecucao

### Posicao
- Id
- ClienteId
- FundoId
- Quantidade

---

# 4. Fluxos Principais

## 4.1 Aplicação

1. Receber requisição
2. Validar saldo do cliente
3. Verificar horário de cut-off
4. Se dentro do horário:
   - Processar imediatamente
5. Se fora do horário:
   - Agendar ordem
6. Atualizar saldo e posição
7. Persistir dados

---

## 4.2 Resgate

1. Receber requisição
2. Validar posição do cliente
3. Verificar horário de cut-off
4. Se dentro do horário:
   - Processar imediatamente
5. Se fora do horário:
   - Agendar ordem
6. Atualizar posição e saldo
7. Persistir dados

---

## 4.3 Processamento de Ordens Agendadas

1. Background Worker executa periodicamente
2. Busca ordens com status "Agendada"
3. Executa validações novamente
4. Processa ordem
5. Atualiza saldo e posição
6. Atualiza status para "Processada"

---

# 5. Regras de Negócio

## Aplicação
- Cliente deve possuir saldo suficiente

## Resgate
- Cliente não pode resgatar mais do que possui

## Cut-off
- Cada fundo possui horário limite
- Ordens fora do horário são agendadas

## Agendamento
- Execução no próximo dia útil

## Consistência
- Todas as operações devem ser transacionais

## Concorrência
- O sistema deve impedir inconsistências em operações simultâneas

---

# 6. Decisões Técnicas

## Persistência
- Banco relacional
- EF Core

## Transações
- Uso de transações para garantir atomicidade

## Concorrência
- Controle otimista utilizando RowVersion
- Tratamento de conflitos

## Background Processing
- HostedService para execução de tarefas agendadas

## Observabilidade
- Logs estruturados
- Tratamento de erros

---

# 7. Estratégias Críticas

## Consistência de Dados
- Operações críticas executadas dentro de transações

## Concorrência
- Uso de controle otimista
- Garantia de integridade em cenários simultâneos

## Processamento Assíncrono
- Separação entre criação e execução de ordens agendadas

---

# 8. Trade-offs

## Não utilização de mensageria (SQS/Kafka)

Motivo:
- Evitar complexidade desnecessária para o escopo do case
- Priorizar clareza e entrega funcional

Impacto:
- Menor desacoplamento
- Menor escalabilidade imediata

---

## Não utilização de AWS Lambda

Motivo:
- Simplificação da execução local e avaliação do código
- Foco em lógica de negócio e consistência

---

## Uso de Background Worker ao invés de arquitetura distribuída

Motivo:
- Simplicidade
- Facilidade de execução

---

# 9. Evolução Futura

A solução pode evoluir para:

- Arquitetura orientada a eventos
- Uso de mensageria (SQS ou Kafka)
- Processamento com AWS Lambda
- Separação em microserviços
- Escalabilidade horizontal
- Monitoramento avançado

---

# 10. Uso de Inteligência Artificial

A Inteligência Artificial foi utilizada como ferramenta de apoio para:

- Geração de estrutura inicial do projeto
- Sugestão de organização arquitetural
- Apoio na criação de código boilerplate

As decisões de arquitetura, modelagem e regras de negócio foram conduzidas de forma consciente, garantindo aderência aos requisitos do problema.

---

# 11. Conclusão

A solução proposta atende aos requisitos funcionais e não funcionais do problema, garantindo:

- Consistência dos dados
- Controle de concorrência
- Clareza arquitetural
- Facilidade de evolução

A abordagem adotada equilibra simplicidade e robustez, permitindo evolução futura para cenários mais complexos.