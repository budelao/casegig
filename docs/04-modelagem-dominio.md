# 🧩 Modelagem de Domínio

Este documento define as entidades do domínio do sistema de ordens de investimento, alinhadas com o enunciado do case e com o Diagrama Entidade-Relacionamento (DER).

---

# 🎯 Objetivo

Representar corretamente:

- Clientes e seus saldos
- Fundos de investimento
- Posições dos clientes
- Ordens de aplicação e resgate

Separando claramente:

- Dinheiro (saldo)
- Investimento (cotas)

---

# 🧱 Entidades

---

## 👤 Cliente

Representa o investidor no sistema.

### Propriedades:

- IdCliente (Guid)
- Nome (string)
- Cpf (string)
- SaldoDisponivel (decimal)

---

## 📈 Fundo

Representa um fundo de investimento disponível para aplicação/resgate.

### Propriedades:

- IdFundo (Guid)
- Nome (string)
- HorarioCorte (TimeSpan)
- ValorCota (decimal)
- ValorMinimoAporte (decimal)
- ValorMinimoPermanencia (decimal)
- StatusCaptacao (enum)

### Enum: StatusCaptacao

- ABERTO
- FECHADO

---

## 📊 Posicao

Representa a posição de um cliente em um fundo (quantidade de cotas).

### Propriedades:

- IdCliente (Guid)
- IdFundo (Guid)
- QuantidadeCotas (decimal)

### Observações:

- Chave composta: (IdCliente, IdFundo)
- Representa o saldo investido em cotas

---

## 📄 Ordem

Representa uma solicitação de aplicação ou resgate.

### Propriedades:

- IdOrdem (Guid)
- IdCliente (Guid)
- IdFundo (Guid)
- TipoOperacao (enum)
- QuantidadeCotas (decimal)
- DataAgendamento (DateTime?) *(nullable)*
- Status (enum)

---

# 🔁 Enums

---

## TipoOperacao

- APORTE
- RESGATE

---

## StatusOrdem

- CRIADA
- AGENDADA
- EM_PROCESSAMENTO
- CONCLUIDA
- CANCELADA
- REJEITADA

---

# 🔗 Relacionamentos

- Cliente 1:N Ordem
- Fundo 1:N Ordem
- Cliente 1:N Posicao
- Fundo 1:N Posicao

---

# ⚙️ Regras implícitas no modelo

- Cliente possui saldo financeiro separado das cotas
- Fundo define regras de investimento (mínimos, status, valor da cota)
- Posicao controla quantidade de cotas por cliente/fundo
- Ordem representa intenção de operação, não execução imediata

---

# 🧠 Observações Importantes

## 💰 Separação entre dinheiro e investimento

- SaldoDisponivel → dinheiro em conta
- QuantidadeCotas → investimento em fundos

---

## 🔄 Conversão de valores

- Aplicação:
  valor financeiro → cotas  
  quantidade_cotas = valor / valor_cota

- Resgate:
  cotas → valor financeiro  
  valor = quantidade_cotas * valor_cota

---

## ⏱️ Cut-off

- Ordens podem ser:
  - Processadas imediatamente
  - Agendadas para execução posterior

---

## 📌 Consistência com o DER

Este modelo está alinhado com:

- Campos exigidos no case
- Relacionamentos definidos
- Estrutura do banco de dados

---

# 🎯 Conclusão

A modelagem foi construída para:

- Representar corretamente o domínio financeiro
- Permitir implementação das regras do case
- Garantir consistência entre dados e operações
- Facilitar evolução da solução