## 🔹 Etapa 11 - Worker (Processamento de Ordens Agendadas)

Prompt:

Considere os arquivos:
- 04-modelagem-dominio.md
- 05-regras-negocio.md
- 06-estrategia-tecnica.md

Implemente um Background Worker (HostedService) responsável pelo processamento de ordens agendadas.

---

## 🎯 Objetivo

Executar automaticamente ordens com:

- Status = AGENDADA
- DataAgendamento <= Data/Hora atual

---

## ⚙️ Funcionamento

O worker deve:

1. Rodar periodicamente (ex: a cada 30 segundos)
2. Buscar ordens elegíveis para execução
3. Processar cada ordem
4. Atualizar status da ordem

---

## 🔎 Consulta de Ordens

Buscar no banco:

- Status = AGENDADA
- DataAgendamento <= Now()

---

## 🔁 Fluxo de Processamento

Para cada ordem:

### 1. Carregar dados necessários
- Cliente
- Fundo
- Posição

---

### 2. Revalidar regras de negócio

IMPORTANTE: Todas as regras devem ser revalidadas no momento da execução.

#### Aplicação:
- Validar saldo disponível (AGORA)
- Validar fundo aberto
- Validar valores mínimos

#### Resgate:
- Validar quantidade de cotas atual
- Validar permanência mínima

---

### 3. Executar operação

#### Aplicação:
- Debitar saldo do cliente
- Converter valor em cotas
- Atualizar ou criar posição

#### Resgate:
- Debitar cotas da posição
- Converter cotas em valor
- Creditar saldo do cliente

---

### 4. Atualizar status da ordem

- CONCLUIDA → sucesso
- REJEITADA → falha na revalidação

---

## 🔒 Concorrência

- Utilizar controle otimista (RowVersion)
- Garantir consistência dos dados
- Evitar processamento duplicado

---

## ♻️ Idempotência

Antes de processar:

- Verificar se a ordem ainda está com status AGENDADA
- Evitar reprocessamento

---

## 🧾 Logs

Registrar:

- Início do processamento
- Ordem sendo processada
- Sucesso
- Erros

---

## ⚠️ Tratamento de Erros

- Capturar exceções
- Não interromper processamento das demais ordens
- Marcar ordem como REJEITADA quando necessário

---

## 🧠 Boas Práticas

- Processar ordens de forma isolada
- Evitar transações muito longas
- Separar lógica de domínio da infraestrutura
- Manter o worker leve e resiliente

---

## 🚀 Resultado Esperado

O sistema deve ser capaz de:

- Executar automaticamente ordens agendadas
- Garantir consistência de dados
- Revalidar regras no momento correto
- Atualizar corretamente saldo e posições