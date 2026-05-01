using CaseGig.Domain.Entities;
using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;

namespace CaseGig.Domain.Services;

public sealed class OrdemService
{
    public Ordem CriarOrdemAporte(Cliente cliente, Fundo fundo, decimal valorAporte, DateTime agora)
    {
        ValidarFundoAberto(fundo);

        if (valorAporte <= 0)
        {
            throw new BusinessRuleException("Valor de aporte deve ser maior que zero.");
        }

        if (valorAporte < fundo.ValorMinimoAporte)
        {
            throw new BusinessRuleException("Valor de aporte abaixo do mínimo permitido para o fundo.");
        }

        if (cliente.SaldoDisponivel < valorAporte)
        {
            throw new BusinessRuleException("Saldo insuficiente para realizar o aporte.");
        }

        var quantidadeCotas = valorAporte / fundo.ValorCota;
        var ordem = NovaOrdemBase(cliente.IdCliente, fundo.IdFundo, TipoOperacao.APORTE, quantidadeCotas, agora);

        if (EstaForaDoCutoff(fundo, agora))
        {
            ordem.Status = StatusOrdem.AGENDADA;
            ordem.DataAgendamento = ProximoDiaUtil(agora.Date);
        }

        return ordem;
    }

    public Ordem CriarOrdemResgate(
        Cliente cliente,
        Fundo fundo,
        Posicao? posicao,
        decimal quantidadeCotas,
        DateTime agora)
    {
        ValidarFundoAberto(fundo);

        if (quantidadeCotas <= 0)
        {
            throw new BusinessRuleException("Quantidade de cotas para resgate deve ser maior que zero.");
        }

        if (posicao is null || posicao.QuantidadeCotas < quantidadeCotas)
        {
            throw new BusinessRuleException("Cotas insuficientes para realizar o resgate.");
        }

        var cotasRestantes = posicao.QuantidadeCotas - quantidadeCotas;
        var valorRestante = cotasRestantes * fundo.ValorCota;
        if (cotasRestantes > 0 && valorRestante < fundo.ValorMinimoPermanencia)
        {
            throw new BusinessRuleException("Resgate viola o valor mínimo de permanência do fundo.");
        }

        var ordem = NovaOrdemBase(cliente.IdCliente, fundo.IdFundo, TipoOperacao.RESGATE, quantidadeCotas, agora);

        if (EstaForaDoCutoff(fundo, agora))
        {
            ordem.Status = StatusOrdem.AGENDADA;
            ordem.DataAgendamento = ProximoDiaUtil(agora.Date);
        }

        return ordem;
    }

    private static void ValidarFundoAberto(Fundo fundo)
    {
        if (fundo.StatusCaptacao != StatusCaptacao.ABERTO)
        {
            throw new BusinessRuleException("Fundo está FECHADO para novas operações.");
        }
    }

    private static Ordem NovaOrdemBase(Guid idCliente, Guid idFundo, TipoOperacao tipoOperacao, decimal quantidadeCotas, DateTime agora)
    {
        return new Ordem
        {
            IdOrdem = Guid.NewGuid(),
            IdCliente = idCliente,
            IdFundo = idFundo,
            TipoOperacao = tipoOperacao,
            QuantidadeCotas = quantidadeCotas,
            DataCriacao = agora,
            Status = StatusOrdem.CRIADA
        };
    }

    private static bool EstaForaDoCutoff(Fundo fundo, DateTime agora)
    {
        return agora.TimeOfDay > fundo.HorarioCorte;
    }

    private static DateTime ProximoDiaUtil(DateTime dataBase)
    {
        var data = dataBase.AddDays(1);
        while (data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            data = data.AddDays(1);
        }

        return data;
    }
}
