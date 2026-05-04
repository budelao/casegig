using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;

namespace CaseGig.Domain.Entities;

public sealed class Ordem
{
    public Guid IdOrdem { get; set; }
    public Guid IdCliente { get; set; }
    public Guid IdFundo { get; set; }
    public TipoOperacao TipoOperacao { get; set; }
    public decimal QuantidadeCotas { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataAgendamento { get; set; }
    public DateTime? DataProcessamento { get; set; }
    public StatusOrdem Status { get; set; }
    public long RowVersion { get; set; }

    public Cliente? Cliente { get; set; }
    public Fundo? Fundo { get; set; }

    public void Agendar(DateTime dataExecucao)
    {
        Status = StatusOrdem.AGENDADA;
        DataAgendamento = dataExecucao;
    }

    public void PrepararParaProcessamento(DateTime agora)
    {
        if (Status is StatusOrdem.CONCLUIDA or StatusOrdem.REJEITADA or StatusOrdem.CANCELADA)
        {
            throw new BusinessRuleException("Ordem não pode ser processada no status atual.");
        }

        if (Status == StatusOrdem.AGENDADA && DataAgendamento.HasValue && agora.Date < DataAgendamento.Value.Date)
        {
            throw new BusinessRuleException("Ordem ainda não está elegível para processamento.");
        }

        Status = StatusOrdem.EM_PROCESSAMENTO;
    }

    public void Concluir(DateTime agora)
    {
        Status = StatusOrdem.CONCLUIDA;
        DataProcessamento = agora;
        DataAgendamento = null;
    }

    public void Rejeitar(DateTime agora)
    {
        Status = StatusOrdem.REJEITADA;
        DataProcessamento = agora;
    }

    public void Processar(Cliente cliente, Fundo fundo, Posicao? posicao, DateTime agora)
    {
        PrepararParaProcessamento(agora);
        fundo.GarantirAbertoParaOperacoes();

        switch (TipoOperacao)
        {
            case TipoOperacao.APORTE:
                ProcessarAporte(cliente, fundo, posicao);
                break;
            case TipoOperacao.RESGATE:
                if (posicao is null)
                {
                    throw new BusinessRuleException("Cotas insuficientes para realizar o resgate.");
                }

                ProcessarResgate(cliente, fundo, posicao);
                break;
            default:
                throw new BusinessRuleException("Tipo de operação inválido.");
        }

        Concluir(agora);
    }

    internal static Ordem NovaOrdemBase(Guid idCliente, Guid idFundo, TipoOperacao tipoOperacao, decimal quantidadeCotas, DateTime agora)
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

    private void ProcessarAporte(Cliente cliente, Fundo fundo, Posicao? posicao)
    {
        var valorOperacao = QuantidadeCotas * fundo.ValorCota;

        if (QuantidadeCotas < fundo.ValorMinimoAporte)
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

        posicao.CreditarCotas(QuantidadeCotas);
    }

    private void ProcessarResgate(Cliente cliente, Fundo fundo, Posicao posicao)
    {
        if (posicao.QuantidadeCotas < QuantidadeCotas)
        {
            throw new BusinessRuleException("Cotas insuficientes para realizar o resgate.");
        }

        var cotasRestantes = posicao.QuantidadeCotas - QuantidadeCotas;
        if (cotasRestantes > 0 && cotasRestantes < fundo.ValorMinimoPermanencia)
        {
            throw new BusinessRuleException("Resgate viola o mínimo de permanência do fundo.");
        }

        var valorOperacao = QuantidadeCotas * fundo.ValorCota;

        posicao.DebitarCotas(QuantidadeCotas);
        cliente.CreditarSaldo(valorOperacao);
    }
}
