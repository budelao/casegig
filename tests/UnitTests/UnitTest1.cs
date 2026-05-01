using CaseGig.Domain.Entities;
using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;
using CaseGig.Domain.Services;

namespace CaseGig.UnitTests;

public sealed class OrdemRulesTests
{
    private readonly OrdemService _ordemService = new();
    private readonly OrdemProcessamentoService _processamentoService = new();

    [Fact]
    public void CriarOrdemAporte_DeveRejeitar_QuandoSaldoInsuficiente()
    {
        var cliente = NovoCliente(100m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 1m);

        var ex = Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemAportePorCotas(cliente, fundo, 20m, HojeAs(10, 0)));
        Assert.Contains("Saldo insuficiente", ex.Message);
    }

    [Fact]
    public void CriarOrdemResgate_DeveRejeitar_QuandoCotasInsuficientes()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoPermanencia: 0m);
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 5m };

        var ex = Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemResgate(cliente, fundo, posicao, 10m, HojeAs(10, 0)));
        Assert.Contains("Cotas insuficientes", ex.Message);
    }

    [Fact]
    public void CriarOrdem_DeveRespeitar_Cutoff()
    {
        var cliente = NovoCliente(10000m);
        var fundo = NovoFundoAberto(cutoff: new TimeSpan(14, 0, 0), valorCota: 10m, valorMinimoAporte: 1m);

        var dentro = _ordemService.CriarOrdemAportePorCotas(cliente, fundo, 10m, HojeAs(13, 0));
        Assert.Equal(StatusOrdem.CRIADA, dentro.Status);
        Assert.Null(dentro.DataAgendamento);

        Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemAportePorCotas(cliente, fundo, 10m, HojeAs(15, 0)));
    }

    [Fact]
    public void CriarOrdemAgendada_DeveValidar_DataUtilEFutura()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 100m);

        Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemAgendadaAporte(cliente, fundo, 10m, HojeAs(0, 0), HojeAs(10, 0)));
        Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemAgendadaAporte(cliente, fundo, 10m, new DateTime(2026, 5, 2), HojeAs(10, 0)));
    }

    [Fact]
    public void CriarOrdemAgendadaAporte_NaoDeveValidar_SaldoNoMomentoDoAgendamento()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 100m);

        var ordem = _ordemService.CriarOrdemAgendadaAporte(cliente, fundo, 100m, new DateTime(2026, 5, 4), HojeAs(10, 0));
        Assert.Equal(StatusOrdem.AGENDADA, ordem.Status);
        Assert.Equal(new DateTime(2026, 5, 4), ordem.DataAgendamento);
        Assert.Equal(100m, ordem.QuantidadeCotas);
    }

    [Fact]
    public void CriarOrdemAporte_DeveRejeitar_QuandoValorAporteAbaixoDoMinimo()
    {
        var cliente = NovoCliente(10000m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 100m);

        var ex = Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemAportePorCotas(cliente, fundo, 5m, HojeAs(10, 0)));
        Assert.Contains("abaixo do mínimo", ex.Message);
    }

    [Fact]
    public void CriarOrdemAporte_DeveRejeitar_QuandoQuantidadeCotasAbaixoDoMinimoDoFundo()
    {
        var cliente = NovoCliente(10000m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 100m);

        Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemAportePorCotas(cliente, fundo, 50m, HojeAs(10, 0)));
    }

    [Fact]
    public void CriarOrdemResgate_DeveRejeitar_QuandoViolaPermanenciaMinima()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoPermanencia: 50m);
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 10m };

        var ex = Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemResgate(cliente, fundo, posicao, 6m, HojeAs(10, 0)));
        Assert.Contains("permanência", ex.Message);
    }

    [Fact]
    public void CriarOrdem_DeveRejeitar_QuandoFundoFechado()
    {
        var cliente = NovoCliente(10000m);
        var fundo = NovoFundoFechado(valorCota: 10m, valorMinimoAporte: 100m);

        Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemAportePorCotas(cliente, fundo, 10m, HojeAs(10, 0)));

        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 10m };
        Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemResgate(cliente, fundo, posicao, 1m, HojeAs(10, 0)));
    }

    [Fact]
    public void ProcessarOrdemAporte_DeveAtualizar_SaldoEPosicao()
    {
        var cliente = NovoCliente(1000m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 1m);

        var ordem = _ordemService.CriarOrdemAportePorCotas(cliente, fundo, 10m, HojeAs(10, 0));
        _processamentoService.PrepararParaProcessamento(ordem, HojeAs(10, 0));
        _processamentoService.ProcessarOrdemAporte(ordem, cliente, fundo, posicao: null);
        _processamentoService.Concluir(ordem, HojeAs(10, 0));

        Assert.Equal(StatusOrdem.CONCLUIDA, ordem.Status);
        Assert.Equal(900m, cliente.SaldoDisponivel);
        Assert.Single(cliente.Posicoes);
        Assert.Equal(10m, cliente.Posicoes.First().QuantidadeCotas);
    }

    [Fact]
    public void ProcessarOrdemResgate_DeveAtualizar_SaldoEPosicao()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoPermanencia: 0m);
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 20m };

        var ordem = _ordemService.CriarOrdemResgate(cliente, fundo, posicao, 5m, HojeAs(10, 0));
        _processamentoService.PrepararParaProcessamento(ordem, HojeAs(10, 0));
        _processamentoService.ProcessarOrdemResgate(ordem, cliente, fundo, posicao);
        _processamentoService.Concluir(ordem, HojeAs(10, 0));

        Assert.Equal(StatusOrdem.CONCLUIDA, ordem.Status);
        Assert.Equal(50m, cliente.SaldoDisponivel);
        Assert.Equal(15m, posicao.QuantidadeCotas);
    }

    private static Cliente NovoCliente(decimal saldo)
    {
        return new Cliente
        {
            IdCliente = Guid.NewGuid(),
            Nome = "Cliente Teste",
            Cpf = "00000000000",
            SaldoDisponivel = saldo
        };
    }

    private static Fundo NovoFundoAberto(
        TimeSpan? cutoff = null,
        decimal valorCota = 10m,
        decimal valorMinimoAporte = 0m,
        decimal valorMinimoPermanencia = 0m)
    {
        return new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo Teste",
            HorarioCorte = cutoff ?? new TimeSpan(14, 0, 0),
            ValorCota = valorCota,
            ValorMinimoAporte = valorMinimoAporte,
            ValorMinimoPermanencia = valorMinimoPermanencia,
            StatusCaptacao = StatusCaptacao.ABERTO
        };
    }

    private static Fundo NovoFundoFechado(decimal valorCota, decimal valorMinimoAporte)
    {
        return new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo Fechado",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = valorCota,
            ValorMinimoAporte = valorMinimoAporte,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.FECHADO
        };
    }

    private static DateTime HojeAs(int hora, int minuto)
    {
        var baseDate = new DateTime(2026, 5, 1);
        return new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, hora, minuto, 0);
    }
}
