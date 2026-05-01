using CaseGig.Domain.Entities;
using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;

namespace CaseGig.Domain.Services;

public sealed class OrdemProcessamentoService
{
    public void PrepararParaProcessamento(Ordem ordem, DateTime agora)
    {
        if (ordem.Status == StatusOrdem.CONCLUIDA || ordem.Status == StatusOrdem.REJEITADA || ordem.Status == StatusOrdem.CANCELADA)
        {
            throw new BusinessRuleException("Ordem não pode ser processada no status atual.");
        }

        if (ordem.Status == StatusOrdem.AGENDADA && ordem.DataAgendamento.HasValue && agora.Date < ordem.DataAgendamento.Value.Date)
        {
            throw new BusinessRuleException("Ordem ainda não está elegível para processamento.");
        }

        ordem.Status = StatusOrdem.EM_PROCESSAMENTO;
    }

    public void ProcessarOrdemAporte(Ordem ordem, Cliente cliente, Fundo fundo, Posicao? posicao)
    {
        if (ordem.TipoOperacao != TipoOperacao.APORTE)
        {
            throw new BusinessRuleException("Tipo de operação inválido para processamento de aporte.");
        }

        if (fundo.StatusCaptacao != StatusCaptacao.ABERTO)
        {
            throw new BusinessRuleException("Fundo está FECHADO para novas operações.");
        }

        var valorOperacao = ordem.QuantidadeCotas * fundo.ValorCota;

        if (ordem.QuantidadeCotas < fundo.ValorMinimoAporte)
        {
            throw new BusinessRuleException("Quantidade de cotas abaixo do mínimo permitido para o fundo.");
        }

        if (cliente.SaldoDisponivel < valorOperacao)
        {
            throw new BusinessRuleException("Saldo insuficiente para realizar o aporte.");
        }

        cliente.DebitarSaldo(valorOperacao);

        if (posicao is null)
        {
            posicao = new Posicao
            {
                IdCliente = cliente.IdCliente,
                IdFundo = fundo.IdFundo,
                QuantidadeCotas = 0
            };

            cliente.Posicoes.Add(posicao);
            fundo.Posicoes.Add(posicao);
        }

        posicao.CreditarCotas(ordem.QuantidadeCotas);
    }

    public void ProcessarOrdemResgate(Ordem ordem, Cliente cliente, Fundo fundo, Posicao posicao)
    {
        if (ordem.TipoOperacao != TipoOperacao.RESGATE)
        {
            throw new BusinessRuleException("Tipo de operação inválido para processamento de resgate.");
        }

        if (fundo.StatusCaptacao != StatusCaptacao.ABERTO)
        {
            throw new BusinessRuleException("Fundo está FECHADO para novas operações.");
        }

        if (posicao.QuantidadeCotas < ordem.QuantidadeCotas)
        {
            throw new BusinessRuleException("Cotas insuficientes para realizar o resgate.");
        }

        var cotasRestantes = posicao.QuantidadeCotas - ordem.QuantidadeCotas;
        if (cotasRestantes > 0 && cotasRestantes < fundo.ValorMinimoPermanencia)
        {
            throw new BusinessRuleException("Resgate viola o mínimo de permanência do fundo.");
        }

        var valorOperacao = ordem.QuantidadeCotas * fundo.ValorCota;

        posicao.DebitarCotas(ordem.QuantidadeCotas);
        cliente.CreditarSaldo(valorOperacao);
    }

    public void Concluir(Ordem ordem, DateTime agora)
    {
        ordem.Status = StatusOrdem.CONCLUIDA;
        ordem.DataProcessamento = agora;
        ordem.DataAgendamento = null;
    }

    public void Rejeitar(Ordem ordem, DateTime agora)
    {
        ordem.Status = StatusOrdem.REJEITADA;
        ordem.DataProcessamento = agora;
    }
}
