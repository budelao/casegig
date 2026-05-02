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

### Pré-requisitos

- .NET 8 SDK
- MySQL 8.x acessível (ex.: `localhost:3306`)

### Configuração

Configure a connection string do MySQL via variável de ambiente (recomendado) ou em `appsettings.Development.json` (não versionar segredos).

- Chave: `ConnectionStrings:MySql`
- Observação: o projeto valida e falha no startup se a connection string estiver com `CHANGE_ME`.

Exemplo (PowerShell):

```powershell
$env:ConnectionStrings__MySql = "server=localhost;port=3306;database=casegig;user=root;password=SUA_SENHA;AllowUserVariables=True"
```

### Build / Execução

Restaurar / compilar:

```bash
dotnet restore
dotnet build
```

Rodar a API:

```bash
dotnet run --project ./src/Api/CaseGig.Api.csproj
```

Em ambiente `Development`, a API tenta aplicar migrations automaticamente no startup.

## Endpoints

Base URL (launchSettings padrão): `http://localhost:5196`

### Criar ordem (imediata)

`POST /ordens`

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

### Criar ordem (agendada)

`POST /ordens/agendamento`

Observação:

- `dataAgendamento` é uma data (sem hora) no formato `dd/MM/yyyy`.
- A data/hora efetiva de execução é calculada como `dataAgendamento` + `HorarioCorte` do fundo.

Exemplo:

```json
{
  "idCliente": "11111111-1111-1111-1111-111111111111",
  "idFundo": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "tipoOperacao": "APORTE",
  "quantidadeCotas": 100.0,
  "dataAgendamento": "04/05/2026"
}
```

### Consultar ordens

`GET /ordens?idCliente={guid}`

### Consultar posição

`GET /posicoes/{idCliente}`

### Envelope de resposta

As respostas seguem o padrão:

```json
{
  "success": true,
  "data": {},
  "errors": []
}
```

## Swagger

Com `ASPNETCORE_ENVIRONMENT=Development`, o Swagger UI fica em:

- `/swagger`

## Worker (ordens agendadas)

O worker (`HostedService`) processa periodicamente ordens elegíveis:

- `Status = AGENDADA`
- `DataAgendamento <= agora`

Configurações em `appsettings.json`:

- `Worker:IntervalSeconds` (default 30)
- `Worker:BatchSize` (default 20)

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

A aplicação utiliza logs estruturados em formato JSON no console, permitindo rastreabilidade e diagnóstico das operações.

### Logging de Requisições

O middleware registra:

- Início e fim da request (método, rota, status code, tempo)
- `CorrelationId` (retornado também no header `X-Correlation-Id`)

Configuração (em `appsettings.json`):

- `Observability:Logging:Enabled`
- `Observability:Logging:AddCorrelationIdHeader`
- `Observability:Logging:LogRequestHeaders` / `RequestHeaderAllowList`
- `Observability:Logging:LogResponseHeaders` / `ResponseHeaderAllowList`

### Integrações (preparado)

Existe estrutura de configuração para futura exportação para:

- Splunk (HecEndpoint/Token)
- Grafana Loki (LokiEndpoint/Token)
- Datadog (Site/ApiKey)

Por enquanto, os logs continuam indo para o console.

## Uso de IA

- Apoio na criação incremental da solução, alinhado aos documentos em `docs/`
- Geração e refinamento de estrutura, camadas, endpoints e testes
