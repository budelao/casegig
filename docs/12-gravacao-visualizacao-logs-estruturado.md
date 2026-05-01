## 🔹 Etapa 12 - Gravação e Visualização de Logs Estruturados

Prompt:

Considere os arquivos:

* 06-estrategia-tecnica.md
* 07-roadmap-implementacao.md

Implemente logging estruturado na aplicação utilizando o ILogger do .NET, garantindo rastreabilidade das operações e suporte à observabilidade.

---

## 🎯 Objetivo

Permitir:

* Monitoramento da aplicação
* Rastreamento do fluxo de execução
* Diagnóstico de erros
* Simulação de logs compatíveis com ferramentas como Splunk, ELK e CloudWatch

---

## ⚙️ Configuração de Logging

No `Program.cs`, configurar logging estruturado em JSON:

* Remover providers padrão
* Adicionar JsonConsole

Resultado esperado:

* Logs em formato JSON
* Cada log contendo propriedades estruturadas

---

## 🧾 Padrão de Logs

Utilizar sempre logs estruturados, evitando concatenação de string.

Exemplo correto:

* LogInformation com parâmetros nomeados (ex: OrdemId, ClienteId)

Evitar:

* strings concatenadas ou interpoladas sem estrutura

---

## 📍 Pontos obrigatórios de log

### 1. API (Controllers)

* Início da requisição
* Dados principais da operação (ex: ClienteId, FundoId)
* Erros de validação

---

### 2. Application / Serviços

* Início da execução de casos de uso
* Decisões importantes (ex: rejeição de regra de negócio)

---

### 3. Worker (CRÍTICO)

Registrar:

* Início do ciclo de execução
* Quantidade de ordens encontradas
* Ordem sendo processada
* Sucesso no processamento
* Falha no processamento (com exceção)

---

## 🔁 Correlação de Logs (opcional - diferencial)

Adicionar um identificador de correlação (CorrelationId):

* Gerado por requisição
* Propagado nos logs

Objetivo:

* Permitir rastrear toda a execução de ponta a ponta

---

## ⚠️ Tratamento de Erros

* Utilizar LogError com exceção
* Garantir que erros não interrompam o processamento total
* Registrar contexto suficiente para diagnóstico

---

## 🧠 Boas Práticas

* Logs devem ser informativos, mas não excessivos
* Não logar dados sensíveis
* Utilizar níveis corretos:

  * Information → fluxo normal
  * Warning → comportamento inesperado
  * Error → falhas

---

## 🚀 Resultado Esperado

Ao executar a aplicação:

* Logs devem aparecer no console em formato JSON
* Deve ser possível identificar:

  * Qual ordem foi processada
  * Resultado da execução
  * Erros ocorridos

---

## 💬 Observação para README

Adicionar seção:

### Observabilidade

"A aplicação utiliza logs estruturados em formato JSON, permitindo rastreabilidade das operações e fácil integração futura com ferramentas de monitoramento como Splunk, ELK ou CloudWatch."

---

## 🎯 Valor Técnico

Essa implementação demonstra:

* Maturidade em observabilidade
* Preparação para ambientes produtivos
* Capacidade de diagnóstico e suporte
