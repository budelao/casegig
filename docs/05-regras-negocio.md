# ⚙️ Regras de Negócio

Este documento define todas as regras de negócio do sistema de ordens de investimento, conforme especificado no case.

---

# 🎯 Objetivo

Garantir:

- Consistência dos dados
- Validação correta das operações
- Aderência ao domínio financeiro
- Controle de execução de ordens

---

# 📄 Tipos de Operação

## Aplicação (APORTE)

- Cliente investe dinheiro em um fundo
- O valor é convertido em cotas

## Resgate (RESGATE)

- Cliente solicita retirada de cotas
- As cotas são convertidas em dinheiro

---

# 🔄 Conversão de Valores

## Aplicação

- quantidade_cotas = valor_aporte / valor_cota

## Resgate

- valor_resgate = quantidade_cotas * valor_cota

---

# 💰 Regras de Aplicação

Para criar uma ordem de aplicação:

- O cliente deve possuir saldo suficiente
- O valor deve respeitar o valor mínimo de aporte do fundo
- O fundo deve estar com status ABERTO

---

# 📉 Regras de Resgate

Para criar uma ordem de resgate:

- O cliente deve possuir cotas suficientes
- O resgate não pode deixar a posição abaixo do valor mínimo de permanência
- O fundo deve estar com status ABERTO

---

# ⏱️ Regra de Cut-off

- Cada fundo possui um horário limite (horario_corte)

## Comportamento:

- Se a ordem for criada antes do cut-off:
  → Pode ser processada imediatamente

- Se a ordem for criada após o cut-off:
  → Deve ser agendada para o próximo período

---

# 📊 Ciclo de Vida da Ordem

## Status possíveis:

- CRIADA
- AGENDADA
- EM_PROCESSAMENTO
- CONCLUIDA
- CANCELADA
- REJEITADA

---

## Regras de transição:

- CRIADA → AGENDADA (se fora do cut-off)
- CRIADA → EM_PROCESSAMENTO (se dentro do horário)
- AGENDADA → EM_PROCESSAMENTO (quando processada pelo worker)
- EM_PROCESSAMENTO → CONCLUIDA (sucesso)
- EM_PROCESSAMENTO → REJEITADA (falha de validação)
- Ordem pode ser CANCELADA antes do processamento

---

# ⚙️ Processamento de Ordens

Durante o processamento:

## Aplicação:

- Debitar saldo do cliente
- Creditar cotas na posição
- Atualizar status da ordem

## Resgate:

- Debitar cotas da posição
- Creditar saldo ao cliente
- Atualizar status da ordem

---

# 🔁 Revalidação no Processamento

⚠️ IMPORTANTE:

Antes de processar uma ordem AGENDADA, o sistema deve:

- Validar novamente saldo/posição
- Validar status do fundo
- Validar regras de mínimo

Se alguma validação falhar:

→ Ordem deve ser marcada como REJEITADA

---

# 🔒 Consistência e Concorrência

- Operações devem ser atômicas
- Uso de transações obrigatórias
- Evitar inconsistência em saldo e posição

---

# 🔁 Idempotência

- Uma ordem não deve ser processada mais de uma vez
- O sistema deve garantir que reprocessamentos não causem efeitos duplicados

---

# 🚫 Regras de Rejeição

Uma ordem deve ser REJEITADA se:

- Saldo insuficiente
- Cotas insuficientes
- Fundo FECHADO
- Violação de valor mínimo de aporte
- Violação de valor mínimo de permanência
- Falha na validação no momento do processamento

---

# 🧠 Observações

- Regras devem ser centralizadas no domínio
- Não duplicar validações na camada de API
- Garantir clareza e rastreabilidade das decisões