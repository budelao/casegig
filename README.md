# Sistema de Ordens de Investimento (Aporte/Resgate)

API REST para criação e processamento de ordens de investimento em fundos, com:

- Validação de regras de negócio (saldo, posição, mínimos, fundo ABERTO/FECHADO)
- Regra de cut-off por fundo (ordens após o horário são AGENDADAS)
- Processamento assíncrono de ordens AGENDADAS via HostedService
- Persistência com EF Core + MySQL, com controle de concorrência (RowVersion)

## Tecnologias

- .NET 8
- ASP.NET Core (Controllers) + Swagger (OpenAPI)
- EF Core 8 + Pomelo (MySQL)
- xUnit

## Como executar

1) Configure um MySQL acessível (ex.: `localhost:3306`).

2) Ajuste a connection string em [appsettings.json](file:///c:/projetos/CaseGig/src/Api/appsettings.json):

- `ConnectionStrings:MySql`

3) Restaurar / compilar:

```bash
./.dotnet/dotnet restore
./.dotnet/dotnet build
```

4) Rodar a API:

```bash
./.dotnet/dotnet run --project src/Api
```

Em ambiente Development, a API tenta aplicar migrations automaticamente no startup.

## Endpoints

### Criar ordem

`POST /api/ordens`

Exemplo (APORTE por quantidade de cotas):

```json
{
  "idCliente": "11111111-1111-1111-1111-111111111111",
  "idFundo": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "tipoOperacao": "APORTE",
  "quantidadeCotas": 10.0
}
```

Exemplo (RESGATE por quantidade de cotas):

```json
{
  "idCliente": "11111111-1111-1111-1111-111111111111",
  "idFundo": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "tipoOperacao": "RESGATE",
  "quantidadeCotas": 10.0
}
```

### Consultar ordens

`GET /api/ordens?idCliente={guid}`

### Consultar posição

`GET /api/posicoes/{idCliente}`

## Swagger

Com `ASPNETCORE_ENVIRONMENT=Development`, o Swagger UI fica em:

- `/swagger`

## Arquitetura

- **Api**: Controllers, middlewares, worker
- **Application**: UseCases e contratos (abstrações de repositório/transaction)
- **Domain**: Entidades, enums e regras de negócio (serviços de domínio)
- **Infrastructure**: EF Core (DbContext, migrations) e repositórios

## Seed de dados

O seed inicial é criado via migrations e inclui:

- Cliente 1 (saldo alto): `11111111-1111-1111-1111-111111111111`
- Cliente 2 (saldo baixo): `22222222-2222-2222-2222-222222222222`
- Fundo 1 ABERTO: `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`
- Fundo 2 FECHADO: `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb`

## Desenhos

### Arquitetura (conceitual)

![AWS](desenhos/AWS.png)

### DER

![DER](desenhos/DER.png)

## Decisões técnicas e trade-offs

- Banco relacional (MySQL) + EF Core para ACID e consistência
- HostedService para processamento assíncrono (evita mensageria no escopo do case)
- Controle de concorrência otimista com RowVersion

## Observabilidade

A aplicação utiliza logs estruturados em formato JSON, permitindo rastreabilidade das operações e fácil integração futura com ferramentas de monitoramento como Splunk, ELK ou CloudWatch.

### Logging de Requisições

A aplicação possui middleware de logging que captura todas as requisições HTTP, incluindo tempo de execução e identificador de correlação, permitindo rastreamento completo das operações.

## Uso de IA

- Apoio na criação incremental da solução, alinhado aos documentos em `docs/`
- Geração e refinamento de estrutura, camadas, endpoints e testes

## 🔍 Observabilidade

A aplicação implementa logs estruturados em formato JSON, permitindo rastreabilidade e diagnóstico das operações.

### Logging de Requisições

Foi implementado um middleware responsável por capturar todas as requisições HTTP, registrando:

- Método HTTP e endpoint
- Status da resposta
- Tempo de execução
- CorrelationId (identificador único por requisição)

Isso permite rastrear o fluxo completo de uma operação, desde a entrada da requisição até o processamento final.

### Logging de Processamento

O processamento assíncrono de ordens também é monitorado por meio de logs estruturados, incluindo:

- Ordens processadas
- Status da execução (sucesso ou falha)
- Erros ocorridos

### Evolução

Em um cenário produtivo, esses logs poderiam ser integrados com ferramentas como:

- AWS CloudWatch
- ELK Stack
- Splunk
