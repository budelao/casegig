# 🚀 Guia de Execução do Case - Sistema de Ordens de Investimento

Este documento centraliza o passo a passo para construção do sistema utilizando IA (TRAE), com base nos documentos de apoio localizados na pasta /docs.

---

# 🧠 Estratégia de Construção

A construção será feita de forma incremental, guiada por prompts estruturados, garantindo:

- Coerência com o domínio
- Aderência às regras de negócio
- Qualidade arquitetural
- Facilidade de evolução

⚠️ Regra importante:
Nunca gerar o sistema inteiro de uma vez. Sempre evoluir por etapas.

---

# 📂 Documentos de Apoio

Utilizar sempre os seguintes arquivos como contexto:

- 00.1-dados-iniciais.md
- 01-contexto.md
- 02-requisitos.md
- 03-arquitetura.md
- 04-modelagem-dominio.md
- 05-regras-negocio.md
- 06-estrategia-tecnica.md
- 07-roadmap-implementacao.md
- SDD.md

---

# 🏗️ Etapas de Construção

---

## 🔹 Etapa 1 - Estrutura do Projeto

Prompt:

Considere os arquivos:
- 01-contexto.md
- 03-arquitetura.md

Gere uma solução .NET 8 com a seguinte estrutura:

src/
  Api/
  Application/
  Domain/
  Infrastructure/

tests/
  UnitTests/

docs/
  (já existentes)

Requisitos:

- API REST no projeto Api
- Separação clara de responsabilidades entre camadas
- Domain sem dependência de outras camadas
- Application dependendo apenas de Domain
- Infrastructure implementando persistência (EF Core)
- Configuração inicial funcional

Não implementar regras de negócio ainda.
Apenas estrutura e configuração base.

---

## 🔹 Etapa 2 - Modelagem de Domínio

Prompt:

Considere:
- 01-contexto.md
- 04-modelagem-dominio.md

Gere as entidades:

- Cliente
- Fundo
- Ordem
- Posicao

Inclua:
- Tipos corretos
- Enum para TipoOrdem
- Enum para StatusOrdem

---

## 🔹 Etapa 3 - Persistência (EF Core)

Prompt:

Considere:
- 04-modelagem-dominio.md
- 06-estrategia-tecnica.md

Implemente:

- DbContext
- Configuração de entidades
- Relacionamentos
- Campo RowVersion para controle de concorrência

Prepare para migrations

---

## 🔹 Etapa 3.1 - Dados Iniciais (Seed)

Prompt:

Considere:
- 00.1-dados-iniciais.md
- 04-modelagem-dominio.md

Implemente a carga inicial de dados conforme especificado:

- Clientes com saldo disponível
- Fundos com:
  - valor de cota
  - valor mínimo de aporte
  - valor mínimo de permanência
  - status (ABERTO/FECHADO)
- Posições iniciais de cliente em fundos

Objetivo:

- Permitir testes rápidos da API
- Garantir consistência com as regras de negócio
- Facilitar validação dos cenários do case

Pode utilizar:

- Seed via DbContext
- Ou script inicial

---

## 🔹 Etapa 4 - Regras de Negócio (CRÍTICO)

Prompt:

Considere:
- 05-regras-negocio.md
- 06-estrategia-tecnica.md

Implemente serviços para:

- Aplicação (aporte)
- Resgate

Garantindo:

- Validação de saldo do cliente
- Validação de posição (quantidade de cotas)
- Regra de cut-off
- Conversão entre valor financeiro e cotas
- Validação de valor mínimo de aporte
- Validação de valor mínimo de permanência após resgate
- Validação de status do fundo (ABERTO/FECHADO)
- Controle do ciclo de vida da ordem (status)
- Uso de transações
- Consistência dos dados

⚠️ Revisar cuidadosamente esta etapa (é o núcleo do domínio)

---

## 🔹 Etapa 5 - Application Layer

Prompt:

Crie a camada de Application com:

- Services / UseCases
- DTOs
- Orquestração das operações

Sem duplicar regras de negócio

---

## 🔹 Etapa 6 - API (Controllers)

Prompt:

Crie endpoints REST para:

- Criar ordem
- Consultar ordens
- Consultar posição

Seguindo boas práticas HTTP:

- Uso correto de verbos (POST, GET)
- Retornos apropriados (200, 201, 400, 404, 422, 500)
- Validação de entrada

---

## 📄 Requisitos Funcionais

- Endpoint para criação de ordem deve validar dados de entrada
- Endpoint de consulta deve permitir listar ordens por cliente
- Endpoint de posição deve retornar quantidade de cotas por fundo

---

## 📘 Swagger (OpenAPI)

Configurar Swagger para documentação e testes da API:

- Habilitar Swagger UI
- Documentar todos os endpoints
- Incluir exemplos de request/response
- Descrever parâmetros e retornos
- Definir status codes possíveis (200, 201, 400, 422, 500)
- Permitir testes diretamente pela interface web

---

## ⚠️ Tratamento de Erros

Implementar tratamento global de exceções:

- Capturar exceções de negócio e técnicas
- Retornar respostas padronizadas
- Evitar exposição de detalhes internos do sistema
- Garantir mensagens claras e objetivas

---

## 📦 Padronização de Resposta

Padronizar estrutura de resposta da API:

### Sucesso:

{
  "success": true,
  "data": {},
  "errors": []
}

### Erro:

{
  "success": false,
  "data": null,
  "errors": ["Mensagem de erro"]
}

---

## 🧠 Boas Práticas

- Controllers devem ser leves (sem regra de negócio)
- Utilizar DTOs para entrada e saída
- Delegar processamento para camada de Application
- Garantir legibilidade e organização dos endpoints

---

## 🔹 Etapa 7 - Background Worker

Prompt:

Considere:
- 05-regras-negocio.md

Crie um HostedService que:

- Execute periodicamente
- Busque ordens com status "AGENDADA"
- Revalide regras de negócio antes de processar
- Processe ordens
- Atualize saldo e posição
- Atualize status corretamente

---

## 🔹 Etapa 8 - Concorrência (REFINAMENTO)

Prompt:

Implemente:

- Controle otimista (RowVersion)
- Tratamento de conflitos
- Garantia de consistência

Garantindo que:

- Duas aplicações simultâneas não utilizem o mesmo saldo
- Dois resgates simultâneos não gerem inconsistência de posição

---

## 🔹 Etapa 9 - Logs

Prompt:

Implemente logging estruturado utilizando ILogger:

- Log de entrada de requisições (endpoint + payload resumido)
- Log de criação de ordens
- Log de validações de negócio (quando falhar)
- Log de processamento de ordens (sucesso e erro)
- Log no background worker

Boas práticas:

- Não logar dados sensíveis (ex: CPF completo)
- Usar níveis corretos (Information, Warning, Error)
- Incluir identificador da ordem nos logs

---

## 🔹 Etapa 10 - Testes

Prompt:

Considere:
- 05-regras-negocio.md

Crie testes unitários utilizando xUnit para validar:

- Aplicação com saldo insuficiente
- Resgate com posição insuficiente
- Regra de cut-off (dentro e fora do horário)
- Validação de valor mínimo de aporte
- Validação de valor mínimo de permanência
- Fundo com status FECHADO
- Processamento de ordens (atualização de saldo e posição)

Boas práticas:

- Nome dos testes descritivo
- Padrão Arrange / Act / Assert
- Isolamento de dependências (mocks ou InMemory)
- Foco nas regras críticas do domínio

---

## 🔹 Etapa 11 - README.md

Prompt:

Com base nos arquivos:
- 01-contexto.md
- 03-arquitetura.md
- SDD.md

Crie um README.md completo contendo:

## 1. Descrição do projeto
Resumo do problema e solução

## 2. Tecnologias utilizadas
- .NET 8
- EF Core
- Banco relacional
- xUnit

## 3. Como executar

- dotnet restore
- dotnet build
- dotnet run

Se necessário, incluir instruções de banco

## 4. Endpoints

- Criar ordem
- Consultar ordens
- Consultar posição

## 5. Arquitetura
Resumo da solução

## 6. Arquitetura em nuvem (desenho conceitual)

## 7. DER (Diagrama Entidade-Relacionamento)

## 8. Decisões técnicas

## 9. Trade-offs

## 10. Uso de IA

O README deve ser claro e facilitar a avaliação da banca

---

## 🔹 Etapa 12 - Refinamento Final (CRÍTICO)

Prompt:

Considere TODOS os arquivos do projeto e o SDD.md.

Revise e refatore o código gerado garantindo:

---

### 1. Qualidade de Código

- Separar responsabilidades corretamente
- Evitar classes muito grandes (God Services)
- Melhorar nomes de métodos e variáveis
- Aplicar boas práticas de Clean Code

---

### 2. Domínio

- Garantir uso correto de QuantidadeCotas
- Garantir que não existe uso de "Valor" indevido
- Centralizar lógica de conversão (cotas ↔ valor)
- Evitar lógica de negócio na camada de API

---

### 3. Regras de Negócio

Validar que TODAS estão implementadas:

- Saldo insuficiente
- Cotas insuficientes
- Fundo FECHADO
- Valor mínimo de aporte
- Valor mínimo de permanência
- Cut-off
- Revalidação no processamento

---

### 4. Worker

Refatorar o worker garantindo:

- Revalidação das regras antes do processamento
- Tratamento de erros
- Atualização correta de status
- Idempotência (não processar ordem duas vezes)

---

### 5. Concorrência

- Garantir uso correto de RowVersion
- Tratar conflitos de concorrência
- Evitar inconsistência de saldo e posição

---

### 6. Testes

Garantir que existem testes para:

- Todas as regras críticas
- Cenários de erro
- Processamento de ordens

---

### 7. Organização

- Código limpo e legível
- Separação clara entre camadas
- Remover código duplicado

---

Não adicionar novas funcionalidades.
Apenas refinar e melhorar a qualidade do código existente.

# 🔁 Fluxo de Execução

Para cada etapa:

1. Executar prompt no TRAE
2. Revisar código gerado
3. Ajustar se necessário
4. Validar funcionamento
5. Prosseguir

---

# 🧠 Revisão Arquitetural

Após etapas principais, executar:

"Revise o código com base no SDD.md e sugira melhorias arquiteturais"

---

# ⚠️ Boas Práticas

- Não pular etapas
- Não gerar tudo de uma vez
- Sempre validar regras de negócio
- Priorizar consistência sobre complexidade
- Manter código limpo e organizado

---

# 🎯 Objetivo Final

Entregar uma solução que demonstre:

- Domínio do problema
- Consistência de dados
- Controle de concorrência
- Clareza arquitetural
- Capacidade de evolução

---

# 🧠 Observação Final

A IA deve ser utilizada como ferramenta de apoio.

As decisões arquiteturais e de negócio devem ser conscientes e alinhadas com o SDD.

Obs. Estruturei a construção utilizando documentação modular em Markdown para guiar o uso de IA, garantindo alinhamento com regras de negócio e decisões arquiteturais