# Case Técnico: Negociação de Fundos de Investimentos

> Processo Seletivo — Analista de Sistemas

API para aporte e resgate de fundos de investimentos, com suporte a operações imediatas (dentro da grade disponível) e agendamento de operações futuras. O sistema mantém controle rigoroso sobre horários e regras de cada produto, além de permitir a consulta completa do histórico de ordens com filtros opcionais por cliente.

---

## Sumário

- [1. Contexto e Modelagem Base](#1-contexto-e-modelagem-base)
- [2. Tipos de Operação e Regras de Negócio](#2-tipos-de-operação-e-regras-de-negócio)
- [3. Entregáveis](#3-entregáveis)
- [4. Critérios Técnicos Avaliados](#4-critérios-técnicos-avaliados)
- [5. Glossário de Termos](#5-glossário-de-termos)

---

## 1. Contexto e Modelagem Base

O candidato deverá modelar o banco de dados que sustenta a operação. Estrutura sugerida:

### Clientes (Saldos)
| Campo | Descrição |
|---|---|
| `id_cliente` | Identificador único do cliente |
| `nome` | Nome do cliente |
| `cpf` | CPF do cliente |
| `saldo_disponivel` | Saldo disponível em conta |

### Catálogo de Fundos
| Campo | Descrição |
|---|---|
| `id_fundo` | Identificador único do fundo |
| `nome` | Nome do fundo |
| `horario_corte` | Horário limite (cut-off) para ordens imediatas |
| `valor_cota` | Valor unitário da cota |
| `valor_minimo_aporte` | Valor mínimo aceito por aporte |
| `valor_minimo_permanencia` | Valor mínimo residual após resgate parcial |
| `status_captacao` | Status do fundo (ABERTO/FECHADO) |

### Posição do Cliente
| Campo | Descrição |
|---|---|
| `id_cliente` | Identificador do cliente |
| `id_fundo` | Identificador do fundo |
| `quantidade_cotas` | Quantidade atual de cotas do cliente no fundo |

### Ordens e Agendamentos
| Campo | Descrição |
|---|---|
| `id_ordem` | Identificador único da ordem |
| `id_cliente` | Identificador do cliente |
| `id_fundo` | Identificador do fundo |
| `tipo_operacao` | `APORTE` ou `RESGATE` |
| `quantidade_cotas` | Quantidade de cotas (INT) |
| `data_agendamento` | Data programada (quando aplicável) |
| `status` | Status atual da ordem |

---

## 2. Tipos de Operação e Regras de Negócio

### Conceitos do Sistema

- **Ordem Imediata:** Negociação para o dia atual com validação síncrona.
- **Agendamento:** Intenção programada exclusivamente para uma data futura (D+1 adiante).
- **Janela de Tempo (Cut-off):** Horário limite diário para aceitar ordens imediatas.
- **Data Útil:** Restrição para dias úteis bancários (segunda a sexta).

### Regras de Aplicação (Aporte Imediato)

- **Saldo em Conta:** Recusar se `Saldo < (Quantidade * Valor Cota)`.
- **Valor Mínimo:** Recusar se inferior ao `valor_minimo_aporte`.
- **Capacity:** Recusar se o fundo estiver com status `FECHADO`.

### Regras de Resgate (Imediato)

- **Saldo de Permanência:** Se o resgate parcial deixar um saldo remanescente `> 0` e `< valor_minimo_permanencia`, a ordem deve ser rejeitada.

### Regras de Agendamento (Futuros)

- **Data Útil:** Recusar datas passadas, o dia de hoje ou fins de semana.
- **Aplicação:** Não valida saldo agora (apenas no futuro), mas valida Capacity.
- **Resgate:** Validar se o cliente possui as cotas **hoje** em sua posição.

### Regra Transversal

- **Janela de Tempo:** Ordens imediatas recebidas após o `horario_corte` devem ser recusadas.

---

## 3. Entregáveis

### [Entregável 1] Arquitetura e Dados

Desenho de solução na nuvem e Diagrama Entidade-Relacionamento (MER/DER).

### [Entregável 2] Lógica e Banco de Dados

| Método | Endpoint | Descrição |
|---|---|---|
| `POST` | `/ordens` | Aplicações e Resgates Imediatos |
| `POST` | `/ordens/agendamento` | Aplicações e Resgates Futuros |
| `GET` | `/ordens` | Consulta com filtro opcional por cliente |

#### Exemplo: Payload Imediato

```json
{
  "id_cliente": 1,
  "id_fundo": 10,
  "tipo_operacao": "APORTE",
  "quantidade_cotas": 150
}
```

#### Exemplo: Payload Agendamento

```json
{
  "id_cliente": 1,
  "id_fundo": 10,
  "tipo_operacao": "APORTE",
  "quantidade_cotas": 150,
  "data_agendamento": "2026-12-01"
}
```

### [Entregável 3] Uso de IA

**Como você utilizou a IA nesse projeto?** Descreva ferramentas e auxílio na implementação.

---

## 4. Critérios Técnicos Avaliados

- **Organização e Estrutura:** Clareza na organização do código e separação de camadas.
- **Boas Práticas:** Aplicação de Clean Code, SOLID e Design Patterns.
- **Testes Unitários:** Cobertura e qualidade dos testes para garantir a corretude das regras.
- **Observabilidade:** Estratégias de logs, métricas e rastreabilidade da aplicação.
- **Segurança:** Proteção de dados financeiros, sanitização e integridade das transações.
- **Modelagem de Dados:** Eficiência do esquema e consistência transacional.
- **Documentação:** Qualidade do README e clareza nas instruções.
- **Resiliência:** Capacidade do sistema lidar com falhas e picos de carga.

> **Observação Importante:** Este case é um norte. Caso você avalie a necessidade de incluir alguma regra ou passo no fluxo (ex.: reserva/remoção de saldo no aporte), sinta-se à vontade para implementar e justificar.

---

## 5. Glossário de Termos

| Termo | Definição |
|---|---|
| **Aporte** | Ato de investir ou aplicar dinheiro em um fundo. |
| **Resgate** | Operação de retirada do dinheiro investido. |
| **Cota** | Menor unidade de um fundo; representa a fração do patrimônio. |
| **Janela de Tempo (Cut-off Time)** | Horário limite diário para aceitação de ordens imediatas. |
| **Capacity** | Status que indica se o fundo está aberto ou fechado para novos investimentos. |
| **Saldo de Permanência** | Valor mínimo residual exigido após um resgate parcial. |
| **Data Útil** | Dias de funcionamento do mercado bancário (segunda a sexta), exceto feriados. |
| **MER/DER** | Diagramas para modelagem de entidades e relacionamentos do banco de dados. |
| **Transações ACID** | Propriedades que garantem a confiabilidade e integridade das operações. |
| **Concorrência (Locks)** | Controle para evitar que processos simultâneos corrompam os mesmos dados. |
| **Observabilidade** | Monitoramento da saúde da aplicação via logs, métricas e rastreio. |
| **Resiliência** | Capacidade de recuperação do sistema sob estresse ou falhas. |
| **Payload** | Corpo de dados (JSON) enviado em uma requisição para a API. |
| **Seed** | Dados fictícios iniciais para popular e testar o banco de dados. |
