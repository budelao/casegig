using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;

namespace CaseGig.Domain.Entities;

public sealed class Cliente
{
    public Guid IdCliente { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public decimal SaldoDisponivel { get; set; }
    public long RowVersion { get; set; }

    public ICollection<Posicao> Posicoes { get; set; } = new List<Posicao>();
    public ICollection<Ordem> Ordens { get; set; } = new List<Ordem>();

    public Ordem CriarOrdemImediata(Fundo fundo, Posicao? posicao, TipoOperacao tipoOperacao, decimal quantidadeCotas, DateTime agora)
    {
        fundo.GarantirAbertoParaOperacoes();
        fundo.GarantirDentroDoCutoff(agora);

        return tipoOperacao switch
        {
            TipoOperacao.APORTE => CriarOrdemImediataAportePorCotas(fundo, quantidadeCotas, agora),
            TipoOperacao.RESGATE => CriarOrdemImediataResgate(fundo, posicao, quantidadeCotas, agora),
            _ => throw new BusinessRuleException("Tipo de operação inválido.")
        };
    }

    public Ordem CriarOrdemAgendada(Fundo fundo, Posicao? posicao, TipoOperacao tipoOperacao, decimal quantidadeCotas, DateOnly dataAgendamento, DateTime agora)
    {
        fundo.GarantirAbertoParaOperacoes();
        fundo.GarantirDataAgendamentoValida(dataAgendamento, agora);

        return tipoOperacao switch
        {
            TipoOperacao.APORTE => CriarOrdemAgendadaAporte(fundo, quantidadeCotas, dataAgendamento, agora),
            TipoOperacao.RESGATE => CriarOrdemAgendadaResgate(fundo, posicao, quantidadeCotas, dataAgendamento, agora),
            _ => throw new BusinessRuleException("Tipo de operação inválido.")
        };
    }

    public void DebitarSaldo(decimal valor)
    {
        if (valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor));
        }

        SaldoDisponivel -= valor;
    }

    public void CreditarSaldo(decimal valor)
    {
        if (valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor));
        }

        SaldoDisponivel += valor;
    }

    private Ordem CriarOrdemImediataAportePorCotas(Fundo fundo, decimal quantidadeCotas, DateTime agora)
    {
        if (quantidadeCotas <= 0)
        {
            throw new BusinessRuleException("Quantidade de cotas deve ser maior que zero.");
        }

        var valorAporte = quantidadeCotas * fundo.ValorCota;

        if (quantidadeCotas < fundo.ValorMinimoAporte)
        {
            throw new BusinessRuleException("Quantidade de cotas abaixo do mínimo permitido para o fundo.");
        }

        if (SaldoDisponivel < valorAporte)
        {
            throw new BusinessRuleException("Saldo insuficiente para realizar o aporte.");
        }

        return Ordem.NovaOrdemBase(IdCliente, fundo.IdFundo, TipoOperacao.APORTE, quantidadeCotas, agora);
    }

    private Ordem CriarOrdemImediataResgate(Fundo fundo, Posicao? posicao, decimal quantidadeCotas, DateTime agora)
    {
        if (quantidadeCotas <= 0)
        {
            throw new BusinessRuleException("Quantidade de cotas para resgate deve ser maior que zero.");
        }

        if (posicao is null || posicao.QuantidadeCotas < quantidadeCotas)
        {
            throw new BusinessRuleException("Cotas insuficientes para realizar o resgate.");
        }

        var cotasRestantes = posicao.QuantidadeCotas - quantidadeCotas;
        if (cotasRestantes > 0 && cotasRestantes < fundo.ValorMinimoPermanencia)
        {
            throw new BusinessRuleException("Resgate viola o mínimo de permanência do fundo.");
        }

        return Ordem.NovaOrdemBase(IdCliente, fundo.IdFundo, TipoOperacao.RESGATE, quantidadeCotas, agora);
    }

    private Ordem CriarOrdemAgendadaAporte(Fundo fundo, decimal quantidadeCotas, DateOnly dataAgendamento, DateTime agora)
    {
        if (quantidadeCotas <= 0)
        {
            throw new BusinessRuleException("Quantidade de cotas deve ser maior que zero.");
        }

        if (quantidadeCotas < fundo.ValorMinimoAporte)
        {
            throw new BusinessRuleException("Quantidade de cotas abaixo do mínimo permitido para o fundo.");
        }

        var dataExecucao = fundo.CalcularDataExecucao(dataAgendamento);
        var ordem = Ordem.NovaOrdemBase(IdCliente, fundo.IdFundo, TipoOperacao.APORTE, quantidadeCotas, agora);
        ordem.Agendar(dataExecucao);
        return ordem;
    }

    private Ordem CriarOrdemAgendadaResgate(Fundo fundo, Posicao? posicao, decimal quantidadeCotas, DateOnly dataAgendamento, DateTime agora)
    {
        if (quantidadeCotas <= 0)
        {
            throw new BusinessRuleException("Quantidade de cotas para resgate deve ser maior que zero.");
        }

        if (posicao is null || posicao.QuantidadeCotas < quantidadeCotas)
        {
            throw new BusinessRuleException("Cotas insuficientes para realizar o resgate.");
        }

        var cotasRestantes = posicao.QuantidadeCotas - quantidadeCotas;
        if (cotasRestantes > 0 && cotasRestantes < fundo.ValorMinimoPermanencia)
        {
            throw new BusinessRuleException("Resgate viola o mínimo de permanência do fundo.");
        }

        var dataExecucao = fundo.CalcularDataExecucao(dataAgendamento);
        var ordem = Ordem.NovaOrdemBase(IdCliente, fundo.IdFundo, TipoOperacao.RESGATE, quantidadeCotas, agora);
        ordem.Agendar(dataExecucao);
        return ordem;
    }
}
