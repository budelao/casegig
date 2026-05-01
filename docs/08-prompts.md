# Prompts

## Estrutura inicial

Com base nos arquivos de contexto, arquitetura e modelagem de domínio,
crie uma solução .NET 8 com:

- API REST
- Separação em camadas (Domain, Application, Infrastructure)
- Uso de EF Core
- Organização limpa e escalável

---

## Entidades

Com base na modelagem de domínio,
gere as entidades com:

- Propriedades
- Tipos corretos
- Configuração para EF Core

---

## Serviços de negócio

Com base nas regras de negócio,
implemente serviços para:

- Criar ordem de aplicação
- Criar ordem de resgate

Garantindo:
- Validações
- Uso de transações
- Consistência

---

## Background Worker

Crie um HostedService que:

- Procure ordens com status "Agendada"
- Execute o processamento
- Atualize saldo e posição

---

## Endpoints

Crie endpoints REST para:

- Criar ordem
- Consultar ordens
- Consultar posição