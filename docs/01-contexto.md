# Contexto

Este sistema tem como objetivo gerenciar ordens de aplicação e resgate em fundos de investimento.

A solução deve permitir:
- Criação de ordens de aplicação e resgate
- Validação de saldo do cliente
- Controle de posição por fundo
- Respeito a horários de cut-off
- Agendamento de ordens fora do horário permitido
- Processamento posterior de ordens agendadas

O sistema deve garantir:
- Consistência dos dados (ACID)
- Controle de concorrência
- Integridade financeira

O domínio é sensível, exigindo precisão nas regras de negócio e tratamento adequado de concorrência.