## 🔹 Etapa 13 - Middleware de Logging (Request/Response)

Prompt:

Considere os arquivos:

* 06-estrategia-tecnica.md
* 12-gravacao-visualizacao-logs-estruturado.md

Implemente um Middleware para logging de requisições HTTP, garantindo rastreabilidade completa das chamadas da API.

---

## 🎯 Objetivo

Capturar e registrar:

* Entrada da requisição
* Dados principais da chamada
* Tempo de execução
* Resultado (sucesso ou erro)

---

## ⚙️ Funcionamento

O middleware deve:

1. Interceptar todas as requisições HTTP
2. Gerar um identificador único (CorrelationId)
3. Registrar dados da requisição
4. Executar o pipeline da API
5. Registrar o resultado e tempo de execução

---

## 📥 Dados da Requisição

Registrar:

* Método HTTP (GET, POST, etc)
* Endpoint acessado
* CorrelationId
* Timestamp

---

## 📤 Dados da Resposta

Registrar:

* Status Code
* Tempo de execução (ms)
* CorrelationId

---

## 🔁 CorrelationId (Diferencial)

* Gerar um GUID por requisição
* Incluir no log
* (Opcional) adicionar no header da resposta

Objetivo:

* Permitir rastreamento completo da requisição

---

## 🧾 Padrão de Log

Utilizar logs estruturados com propriedades nomeadas.

Exemplo esperado:

* "Request iniciada {Method} {Path} {CorrelationId}"
* "Request finalizada {StatusCode} {ElapsedMs}ms {CorrelationId}"

---

## ⚠️ Tratamento de Erros

* Capturar exceções não tratadas
* Registrar erro com contexto
* Repassar exceção para pipeline

---

## 🧠 Boas Práticas

* Não logar dados sensíveis
* Evitar logar corpo completo da requisição (opcional)
* Manter logs objetivos
* Utilizar níveis corretos:

  * Information → fluxo normal
  * Error → exceções

---

## 🔧 Integração

Registrar o middleware no pipeline da API (Program.cs), garantindo que ele execute antes dos controllers.

---

## 🚀 Resultado Esperado

Ao executar a API:

* Cada requisição deve gerar logs estruturados
* Deve ser possível rastrear:

  * Início da requisição
  * Tempo de execução
  * Resultado final
  * CorrelationId

---

## 💬 Observação para README

Adicionar seção:

### Logging de Requisições

"A aplicação possui middleware de logging que captura todas as requisições HTTP, incluindo tempo de execução e identificador de correlação, permitindo rastreamento completo das operações."

---

## 🎯 Valor Técnico

Essa implementação demonstra:

* Observabilidade em nível de API
* Rastreamento de ponta a ponta
* Boas práticas de engenharia para produção
