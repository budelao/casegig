# Estratégia Técnica

## Persistência

- Uso de EF Core
- Banco relacional
- Transações explícitas

## Concorrência

- Uso de controle otimista (RowVersion)
- Tratamento de conflitos

## Transações

- Uso de TransactionScope ou DbContext Transaction
- Garantir atomicidade das operações

## Background Processing

- HostedService para execução de ordens agendadas
- Execução periódica

## Observabilidade

- Logs estruturados
- Registro de erros

## Idempotência

- O sistema deve evitar o processamento duplicado de ordens
- O worker deve garantir que uma ordem não seja processada mais de uma vez