# 🧪 Testes Unitários

Este documento define os cenários de testes unitários do sistema, com foco nas regras de negócio críticas.

---

# 🎯 Objetivo

- Validar regras de negócio
- Garantir consistência do domínio
- Prevenir regressões
- Demonstrar qualidade da solução

---

# 🧰 Tecnologias

- xUnit
- Moq (ou similar)
- Banco em memória (opcional)

---

# 🧪 Estrutura dos Testes

Padrão:

- Arrange (preparação)
- Act (execução)
- Assert (validação)

---

# 📌 Cenários de Teste

---

## ✔️ Aplicação (APORTE)

### Deve criar ordem com sucesso

- Cliente com saldo suficiente
- Fundo ABERTO
- Valor acima do mínimo

---

### Deve rejeitar por saldo insuficiente

- Cliente com saldo baixo

---

### Deve rejeitar por valor mínimo

- Valor menor que valor_minimo_aporte

---

### Deve rejeitar fundo fechado

- Fundo com status FECHADO

---

## ✔️ Resgate (RESGATE)

### Deve criar ordem com sucesso

- Cliente com cotas suficientes

---

### Deve rejeitar por cotas insuficientes

- Cliente com poucas cotas

---

### Deve rejeitar por permanência mínima

- Resgate deixa saldo abaixo do mínimo

---

### Deve rejeitar fundo fechado

- Fundo FECHADO

---

## ✔️ Cut-off

### Deve processar imediatamente

- Ordem criada antes do horário de corte

---

### Deve agendar ordem

- Ordem criada após o horário de corte

---

## ✔️ Processamento de Ordem

### Aplicação

- Debita saldo
- Credita cotas
- Atualiza status para CONCLUIDA

---

### Resgate

- Debita cotas
- Credita saldo
- Atualiza status para CONCLUIDA

---

## ✔️ Revalidação no Worker

### Deve rejeitar se saldo mudou

- Ordem agendada
- Saldo insuficiente no momento do processamento

---

### Deve rejeitar se fundo fechado

- Fundo alterado para FECHADO

---

## ✔️ Concorrência

### Deve evitar inconsistência de saldo

- Duas aplicações simultâneas

---

### Deve evitar inconsistência de cotas

- Dois resgates simultâneos

---

## ✔️ Idempotência

### Não deve processar a mesma ordem duas vezes

- Worker executado duas vezes

---

# 🧠 Boas Práticas

- Testes independentes
- Nomes descritivos
- Foco no comportamento
- Evitar dependência de infraestrutura
- Mockar dependências externas

---

# 🎯 Conclusão

Os testes foram definidos para:

- Cobrir cenários críticos do domínio
- Garantir confiabilidade da solução
- Demonstrar qualidade técnica na implementação