# Arquitetura

A solução será baseada em arquitetura em camadas:

- API (Controllers)
- Application (Serviços e casos de uso)
- Domain (Regras de negócio e entidades)
- Infrastructure (Persistência e integrações)

## Decisões

- API REST em .NET 8
- Banco relacional (MySQL)
- EF Core como ORM
- Background Worker (HostedService) para processamento de ordens agendadas

## Justificativa

Optou-se por uma arquitetura simples e clara, priorizando:
- Facilidade de entendimento
- Facilidade de execução do case
- Organização do código

## Evolução futura

Em um cenário produtivo, a solução poderia evoluir para:

- Arquitetura orientada a eventos
- Uso de mensageria (SQS)
- Processamento com AWS Lambda
- Escalabilidade horizontal