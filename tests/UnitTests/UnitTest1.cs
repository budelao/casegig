using CaseGig.Application.Abstractions;
using CaseGig.Application.DTOs;
using CaseGig.Application.Exceptions;
using CaseGig.Application.Idempotency;
using CaseGig.Application.UseCases;
using Microsoft.Extensions.Logging.Abstractions;
using CaseGig.Domain.Entities;
using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;

namespace CaseGig.UnitTests;

public sealed class OrdemRulesTests
{
    [Fact]
    public void CriarOrdemAporte_DeveRejeitar_QuandoSaldoInsuficiente()
    {
        var cliente = NovoCliente(100m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 1m);

        var ex = Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.APORTE, 20m, HojeAs(10, 0)));
        Assert.Contains("Saldo insuficiente", ex.Message);
    }

    [Fact]
    public void CriarOrdemResgate_DeveRejeitar_QuandoCotasInsuficientes()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoPermanencia: 0m);
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 5m };

        var ex = Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao, TipoOperacao.RESGATE, 10m, HojeAs(10, 0)));
        Assert.Contains("Cotas insuficientes", ex.Message);
    }

    [Fact]
    public void CriarOrdem_DeveRespeitar_Cutoff()
    {
        var cliente = NovoCliente(10000m);
        var fundo = NovoFundoAberto(cutoff: new TimeSpan(14, 0, 0), valorCota: 10m, valorMinimoAporte: 1m);

        var dentro = cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.APORTE, 10m, HojeAs(13, 0));
        Assert.Equal(StatusOrdem.CRIADA, dentro.Status);
        Assert.Null(dentro.DataAgendamento);

        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.APORTE, 10m, HojeAs(15, 0)));
    }

    [Fact]
    public void CriarOrdemAgendada_DeveValidar_DataUtilEFutura()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 100m);

        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemAgendada(fundo, posicao: null, TipoOperacao.APORTE, 10m, DateOnly.FromDateTime(HojeAs(0, 0)), HojeAs(10, 0)));
        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemAgendada(fundo, posicao: null, TipoOperacao.APORTE, 10m, new DateOnly(2026, 5, 2), HojeAs(10, 0)));
    }

    [Fact]
    public void CriarOrdemAgendadaAporte_NaoDeveValidar_SaldoNoMomentoDoAgendamento()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 100m);

        var ordem = cliente.CriarOrdemAgendada(fundo, posicao: null, TipoOperacao.APORTE, 100m, new DateOnly(2026, 5, 4), HojeAs(10, 0));
        Assert.Equal(StatusOrdem.AGENDADA, ordem.Status);
        Assert.Equal(new DateTime(2026, 5, 4, 14, 0, 0), ordem.DataAgendamento);
        Assert.Equal(100m, ordem.QuantidadeCotas);
    }

    [Fact]
    public void CriarOrdemAporte_DeveRejeitar_QuandoValorAporteAbaixoDoMinimo()
    {
        var cliente = NovoCliente(10000m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 100m);

        var ex = Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.APORTE, 5m, HojeAs(10, 0)));
        Assert.Contains("abaixo do mínimo", ex.Message);
    }

    [Fact]
    public void CriarOrdemAporte_DeveRejeitar_QuandoQuantidadeCotasAbaixoDoMinimoDoFundo()
    {
        var cliente = NovoCliente(10000m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 100m);

        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.APORTE, 50m, HojeAs(10, 0)));
    }

    [Fact]
    public void CriarOrdemResgate_DeveRejeitar_QuandoViolaPermanenciaMinima()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoPermanencia: 50m);
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 10m };

        var ex = Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao, TipoOperacao.RESGATE, 6m, HojeAs(10, 0)));
        Assert.Contains("permanência", ex.Message);
    }

    [Fact]
    public void CriarOrdemResgate_DeveRejeitar_QuandoSaldoRemanescenteMaiorQueZeroEFicaAbaixoDoMinimo()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoPermanencia: 50m);
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 200m };

        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao, TipoOperacao.RESGATE, 170m, HojeAs(10, 0)));
    }

    [Fact]
    public void CriarOrdem_DeveRejeitar_QuandoFundoFechado()
    {
        var cliente = NovoCliente(10000m);
        var fundo = NovoFundoFechado(valorCota: 10m, valorMinimoAporte: 100m);

        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.APORTE, 10m, HojeAs(10, 0)));

        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 10m };
        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao, TipoOperacao.RESGATE, 1m, HojeAs(10, 0)));
    }

    [Fact]
    public void ProcessarOrdemAporte_DeveAtualizar_SaldoEPosicao()
    {
        var cliente = NovoCliente(1000m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 1m);

        var ordem = cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.APORTE, 10m, HojeAs(10, 0));
        ordem.Processar(cliente, fundo, posicao: null, HojeAs(10, 0));

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

        var ordem = cliente.CriarOrdemImediata(fundo, posicao, TipoOperacao.RESGATE, 5m, HojeAs(10, 0));
        ordem.Processar(cliente, fundo, posicao, HojeAs(10, 0));

        Assert.Equal(StatusOrdem.CONCLUIDA, ordem.Status);
        Assert.Equal(50m, cliente.SaldoDisponivel);
        Assert.Equal(15m, posicao.QuantidadeCotas);
    }

    [Fact]
    public void ProcessarOrdemResgate_DeveDebitar_ExatamenteQuantidadeInformada()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoPermanencia: 0m);
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 600m };

        var ordem = cliente.CriarOrdemImediata(fundo, posicao, TipoOperacao.RESGATE, 500m, HojeAs(10, 0));
        ordem.Processar(cliente, fundo, posicao, HojeAs(10, 0));

        Assert.Equal(100m, posicao.QuantidadeCotas);
        Assert.Equal(5000m, cliente.SaldoDisponivel);
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

public sealed class IdempotencyUseCaseTests
{
    [Fact]
    public async Task CriarOrdem_ComMesmaIdempotencyKeyEMesmoPayload_DeveRetornarReplayESemCriarNovaOrdem()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 10000m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordens = new InMemoryOrdemRepository();
        var useCase = new CriarOrdemUseCase(
            NullLogger<CriarOrdemUseCase>.Instance,
            new InMemoryTransactionManager(),
            new InMemoryClienteRepository(cliente),
            new InMemoryFundoRepository(fundo),
            new InMemoryPosicaoRepository(),
            ordens,
            new IdempotencyService(ordens));

        var request = new CriarOrdemRequestDto(cliente.IdCliente, fundo.IdFundo, TipoOperacao.APORTE, 10m);

        var first = await useCase.ExecuteAsync(request, agora, "  key-1  ", CancellationToken.None);
        var second = await useCase.ExecuteAsync(request, agora, "key-1", CancellationToken.None);

        Assert.False(first.IsReplay);
        Assert.True(second.IsReplay);
        Assert.Equal(first.Result.IdOrdem, second.Result.IdOrdem);
        Assert.Single(ordens.Items);
        var idempotency = ordens.GetIdempotency(ordens.Items[0].IdOrdem);
        Assert.NotNull(idempotency);
        Assert.Equal("key-1", idempotency!.Key);
        Assert.Equal("POST /ordens", idempotency.Operation);
        Assert.False(string.IsNullOrWhiteSpace(idempotency.RequestHash));
    }

    [Fact]
    public async Task CriarOrdem_ComMesmaIdempotencyKeyEPayloadDiferente_DeveRetornar409()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 10000m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordens = new InMemoryOrdemRepository();
        var useCase = new CriarOrdemUseCase(
            NullLogger<CriarOrdemUseCase>.Instance,
            new InMemoryTransactionManager(),
            new InMemoryClienteRepository(cliente),
            new InMemoryFundoRepository(fundo),
            new InMemoryPosicaoRepository(),
            ordens,
            new IdempotencyService(ordens));

        var request1 = new CriarOrdemRequestDto(cliente.IdCliente, fundo.IdFundo, TipoOperacao.APORTE, 10m);
        var request2 = new CriarOrdemRequestDto(cliente.IdCliente, fundo.IdFundo, TipoOperacao.APORTE, 11m);

        await useCase.ExecuteAsync(request1, agora, "key-2", CancellationToken.None);
        var ex = await Assert.ThrowsAsync<ConcurrencyException>(() => useCase.ExecuteAsync(request2, agora, "key-2", CancellationToken.None));
        Assert.Contains("payload diferente", ex.Message);
        Assert.Single(ordens.Items);
    }

    [Fact]
    public async Task CriarOrdem_SemIdempotencyKey_DeveCriarOrdemNovaEmCadaChamada()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 10000m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordens = new InMemoryOrdemRepository();
        var useCase = new CriarOrdemUseCase(
            NullLogger<CriarOrdemUseCase>.Instance,
            new InMemoryTransactionManager(),
            new InMemoryClienteRepository(cliente),
            new InMemoryFundoRepository(fundo),
            new InMemoryPosicaoRepository(),
            ordens,
            new IdempotencyService(ordens));

        var request = new CriarOrdemRequestDto(cliente.IdCliente, fundo.IdFundo, TipoOperacao.APORTE, 10m);

        var first = await useCase.ExecuteAsync(request, agora, idempotencyKey: null, CancellationToken.None);
        var second = await useCase.ExecuteAsync(request, agora, idempotencyKey: null, CancellationToken.None);

        Assert.False(first.IsReplay);
        Assert.False(second.IsReplay);
        Assert.NotEqual(first.Result.IdOrdem, second.Result.IdOrdem);
        Assert.Equal(2, ordens.Items.Count);
    }

    [Fact]
    public async Task CriarOrdemAgendada_ComMesmaIdempotencyKeyEMesmoPayload_DeveRetornarReplay()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 0m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordens = new InMemoryOrdemRepository();
        var useCase = new CriarOrdemAgendadaUseCase(
            NullLogger<CriarOrdemAgendadaUseCase>.Instance,
            new InMemoryTransactionManager(),
            new InMemoryClienteRepository(cliente),
            new InMemoryFundoRepository(fundo),
            new InMemoryPosicaoRepository(),
            ordens,
            new IdempotencyService(ordens));

        var request = new CriarOrdemAgendamentoRequestDto(cliente.IdCliente, fundo.IdFundo, TipoOperacao.APORTE, 10m, new DateOnly(2026, 5, 5));

        var first = await useCase.ExecuteAsync(request, agora, "key-3", CancellationToken.None);
        var second = await useCase.ExecuteAsync(request, agora, "key-3", CancellationToken.None);

        Assert.False(first.IsReplay);
        Assert.True(second.IsReplay);
        Assert.Equal(first.Result.IdOrdem, second.Result.IdOrdem);
        Assert.Single(ordens.Items);
        var idempotency = ordens.GetIdempotency(ordens.Items[0].IdOrdem);
        Assert.NotNull(idempotency);
        Assert.Equal("POST /ordens/agendamento", idempotency!.Operation);
    }
}

public sealed class CriarOrdemUseCaseFlowTests
{
    [Fact]
    public async Task CriarOrdemAporte_QuandoDentroDoCutoff_DeveConcluirEAtualizarSaldoEPosicao()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 1000m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var posicoes = new InMemoryPosicaoRepository();
        var ordens = new InMemoryOrdemRepository();
        var useCase = new CriarOrdemUseCase(
            NullLogger<CriarOrdemUseCase>.Instance,
            new InMemoryTransactionManager(),
            new InMemoryClienteRepository(cliente),
            new InMemoryFundoRepository(fundo),
            posicoes,
            ordens,
            new IdempotencyService(ordens));

        var request = new CriarOrdemRequestDto(cliente.IdCliente, fundo.IdFundo, TipoOperacao.APORTE, 10m);
        var result = await useCase.ExecuteAsync(request, agora, idempotencyKey: null, CancellationToken.None);

        Assert.False(result.IsReplay);
        Assert.Single(ordens.Items);
        Assert.Equal(StatusOrdem.CONCLUIDA, ordens.Items[0].Status);
        Assert.NotNull(ordens.Items[0].DataProcessamento);
        Assert.Equal(900m, cliente.SaldoDisponivel);
        Assert.Single(cliente.Posicoes);
        Assert.Equal(10m, cliente.Posicoes.Single().QuantidadeCotas);
    }

    [Fact]
    public async Task CriarOrdemResgate_QuandoDentroDoCutoff_DeveConcluirEAtualizarSaldoEPosicao()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 0m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 20m };
        var posicoes = new InMemoryPosicaoRepository(posicao);

        var ordens = new InMemoryOrdemRepository();
        var useCase = new CriarOrdemUseCase(
            NullLogger<CriarOrdemUseCase>.Instance,
            new InMemoryTransactionManager(),
            new InMemoryClienteRepository(cliente),
            new InMemoryFundoRepository(fundo),
            posicoes,
            ordens,
            new IdempotencyService(ordens));

        var request = new CriarOrdemRequestDto(cliente.IdCliente, fundo.IdFundo, TipoOperacao.RESGATE, 5m);
        await useCase.ExecuteAsync(request, agora, idempotencyKey: null, CancellationToken.None);

        Assert.Single(ordens.Items);
        Assert.Equal(StatusOrdem.CONCLUIDA, ordens.Items[0].Status);
        Assert.Equal(50m, cliente.SaldoDisponivel);
        Assert.Equal(15m, posicao.QuantidadeCotas);
    }

    [Fact]
    public async Task CriarOrdem_QuandoForaDoCutoff_DeveFalharESemPersistirOrdem()
    {
        var agora = new DateTime(2026, 5, 1, 15, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 1000m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordens = new InMemoryOrdemRepository();
        var useCase = new CriarOrdemUseCase(
            NullLogger<CriarOrdemUseCase>.Instance,
            new InMemoryTransactionManager(),
            new InMemoryClienteRepository(cliente),
            new InMemoryFundoRepository(fundo),
            new InMemoryPosicaoRepository(),
            ordens,
            new IdempotencyService(ordens));

        var request = new CriarOrdemRequestDto(cliente.IdCliente, fundo.IdFundo, TipoOperacao.APORTE, 10m);
        await Assert.ThrowsAsync<BusinessRuleException>(() => useCase.ExecuteAsync(request, agora, idempotencyKey: null, CancellationToken.None));

        Assert.Empty(ordens.Items);
    }
}

public sealed class ProcessarOrdensAgendadasUseCaseTests
{
    [Fact]
    public async Task ProcessarOrdensAgendadas_QuandoElegivel_DeveConcluirEAtualizarSaldoEPosicao()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 1000m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordem = new Ordem
        {
            IdOrdem = Guid.NewGuid(),
            IdCliente = cliente.IdCliente,
            IdFundo = fundo.IdFundo,
            TipoOperacao = TipoOperacao.APORTE,
            QuantidadeCotas = 10m,
            DataCriacao = agora.AddMinutes(-5),
            DataAgendamento = agora.AddMinutes(-1),
            Status = StatusOrdem.AGENDADA
        };

        var ordens = new InMemoryOrdemRepository(ordem);
        var useCase = new ProcessarOrdensAgendadasUseCase(
            NullLogger<ProcessarOrdensAgendadasUseCase>.Instance,
            new InMemoryTransactionManager(),
            new InMemoryClienteRepository(cliente),
            new InMemoryFundoRepository(fundo),
            new InMemoryPosicaoRepository(),
            ordens);

        var resumo = await useCase.ExecuteAsync(agora, maximo: 10, CancellationToken.None);

        Assert.Equal(1, resumo.Encontradas);
        Assert.Equal(1, resumo.Processadas);
        Assert.Equal(0, resumo.Rejeitadas);
        Assert.Equal(0, resumo.Erros);
        Assert.Equal(StatusOrdem.CONCLUIDA, ordem.Status);
        Assert.Equal(900m, cliente.SaldoDisponivel);
        Assert.Single(cliente.Posicoes);
    }

    [Fact]
    public async Task ProcessarOrdensAgendadas_QuandoSaldoInsuficiente_DeveRejeitar()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 0m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordem = new Ordem
        {
            IdOrdem = Guid.NewGuid(),
            IdCliente = cliente.IdCliente,
            IdFundo = fundo.IdFundo,
            TipoOperacao = TipoOperacao.APORTE,
            QuantidadeCotas = 10m,
            DataCriacao = agora.AddMinutes(-5),
            DataAgendamento = agora.AddMinutes(-1),
            Status = StatusOrdem.AGENDADA
        };

        var ordens = new InMemoryOrdemRepository(ordem);
        var useCase = new ProcessarOrdensAgendadasUseCase(
            NullLogger<ProcessarOrdensAgendadasUseCase>.Instance,
            new InMemoryTransactionManager(),
            new InMemoryClienteRepository(cliente),
            new InMemoryFundoRepository(fundo),
            new InMemoryPosicaoRepository(),
            ordens);

        var resumo = await useCase.ExecuteAsync(agora, maximo: 10, CancellationToken.None);

        Assert.Equal(1, resumo.Encontradas);
        Assert.Equal(0, resumo.Processadas);
        Assert.Equal(1, resumo.Rejeitadas);
        Assert.Equal(0, resumo.Erros);
        Assert.Equal(StatusOrdem.REJEITADA, ordem.Status);
        Assert.Equal(0m, cliente.SaldoDisponivel);
    }

    [Fact]
    public async Task ProcessarOrdensAgendadas_ResgateSemPosicao_DeveRejeitar()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 0m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordem = new Ordem
        {
            IdOrdem = Guid.NewGuid(),
            IdCliente = cliente.IdCliente,
            IdFundo = fundo.IdFundo,
            TipoOperacao = TipoOperacao.RESGATE,
            QuantidadeCotas = 1m,
            DataCriacao = agora.AddMinutes(-5),
            DataAgendamento = agora.AddMinutes(-1),
            Status = StatusOrdem.AGENDADA
        };

        var ordens = new InMemoryOrdemRepository(ordem);
        var useCase = new ProcessarOrdensAgendadasUseCase(
            NullLogger<ProcessarOrdensAgendadasUseCase>.Instance,
            new InMemoryTransactionManager(),
            new InMemoryClienteRepository(cliente),
            new InMemoryFundoRepository(fundo),
            new InMemoryPosicaoRepository(),
            ordens);

        var resumo = await useCase.ExecuteAsync(agora, maximo: 10, CancellationToken.None);

        Assert.Equal(1, resumo.Encontradas);
        Assert.Equal(0, resumo.Processadas);
        Assert.Equal(1, resumo.Rejeitadas);
        Assert.Equal(StatusOrdem.REJEITADA, ordem.Status);
    }
}

public sealed class ConsultarUseCasesTests
{
    [Fact]
    public async Task ConsultarOrdens_DeveMapearResultado()
    {
        var clienteId = Guid.NewGuid();
        var fundoId = Guid.NewGuid();

        var ordem1 = new Ordem
        {
            IdOrdem = Guid.NewGuid(),
            IdCliente = clienteId,
            IdFundo = fundoId,
            TipoOperacao = TipoOperacao.APORTE,
            QuantidadeCotas = 10m,
            DataCriacao = new DateTime(2026, 5, 1, 10, 0, 0),
            Status = StatusOrdem.CONCLUIDA
        };

        var ordem2 = new Ordem
        {
            IdOrdem = Guid.NewGuid(),
            IdCliente = clienteId,
            IdFundo = fundoId,
            TipoOperacao = TipoOperacao.RESGATE,
            QuantidadeCotas = 1m,
            DataCriacao = new DateTime(2026, 5, 1, 11, 0, 0),
            Status = StatusOrdem.REJEITADA
        };

        var repo = new InMemoryOrdemRepository(ordem1, ordem2);
        var useCase = new ConsultarOrdensUseCase(NullLogger<ConsultarOrdensUseCase>.Instance, repo);

        var result = await useCase.ExecuteAsync(clienteId, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.IdOrdem == ordem1.IdOrdem && x.Status == StatusOrdem.CONCLUIDA);
        Assert.Contains(result, x => x.IdOrdem == ordem2.IdOrdem && x.Status == StatusOrdem.REJEITADA);
    }

    [Fact]
    public async Task ConsultarPosicoes_DeveMapearResultado()
    {
        var clienteId = Guid.NewGuid();
        var fundoId = Guid.NewGuid();

        var posicao = new Posicao { IdCliente = clienteId, IdFundo = fundoId, QuantidadeCotas = 123m };
        var repo = new InMemoryPosicaoRepository(posicao);
        var useCase = new ConsultarPosicoesUseCase(NullLogger<ConsultarPosicoesUseCase>.Instance, repo);

        var result = await useCase.ExecuteAsync(clienteId, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(clienteId, result[0].IdCliente);
        Assert.Equal(fundoId, result[0].IdFundo);
        Assert.Equal(123m, result[0].QuantidadeCotas);
    }
}

public sealed class DomainEdgeCaseTests
{
    [Fact]
    public void Cliente_DebitarSaldo_DeveFalhar_QuandoValorInvalido()
    {
        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 10m };
        Assert.Throws<ArgumentOutOfRangeException>(() => cliente.DebitarSaldo(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => cliente.DebitarSaldo(-1m));
    }

    [Fact]
    public void Cliente_CreditarSaldo_DeveFalhar_QuandoValorInvalido()
    {
        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 10m };
        Assert.Throws<ArgumentOutOfRangeException>(() => cliente.CreditarSaldo(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => cliente.CreditarSaldo(-1m));
    }

    [Fact]
    public void Posicao_CreditarDebitarCotas_DeveFalhar_QuandoQuantidadeInvalida()
    {
        var posicao = new Posicao { IdCliente = Guid.NewGuid(), IdFundo = Guid.NewGuid(), QuantidadeCotas = 10m };
        Assert.Throws<ArgumentOutOfRangeException>(() => posicao.CreditarCotas(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => posicao.CreditarCotas(-1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => posicao.DebitarCotas(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => posicao.DebitarCotas(-1m));
    }

    [Fact]
    public void OrdemProcessamento_Preparar_DeveFalhar_QuandoStatusFinal()
    {
        var concluida = new Ordem { Status = StatusOrdem.CONCLUIDA };
        Assert.Throws<BusinessRuleException>(() => concluida.PrepararParaProcessamento(new DateTime(2026, 5, 1, 10, 0, 0)));

        var rejeitada = new Ordem { Status = StatusOrdem.REJEITADA };
        Assert.Throws<BusinessRuleException>(() => rejeitada.PrepararParaProcessamento(new DateTime(2026, 5, 1, 10, 0, 0)));

        var cancelada = new Ordem { Status = StatusOrdem.CANCELADA };
        Assert.Throws<BusinessRuleException>(() => cancelada.PrepararParaProcessamento(new DateTime(2026, 5, 1, 10, 0, 0)));
    }

    [Fact]
    public void OrdemProcessamento_Preparar_DeveFalhar_QuandoAgendadaAindaNaoElegivel()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var ordem = new Ordem
        {
            Status = StatusOrdem.AGENDADA,
            DataAgendamento = agora.AddDays(1)
        };

        var ex = Assert.Throws<BusinessRuleException>(() => ordem.PrepararParaProcessamento(agora));
        Assert.Contains("não está elegível", ex.Message);
    }

    [Fact]
    public void OrdemProcessamento_Concluir_DeveZerarAgendamentoESetarDataProcessamento()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var ordem = new Ordem
        {
            Status = StatusOrdem.EM_PROCESSAMENTO,
            DataAgendamento = agora.AddMinutes(-1)
        };

        ordem.Concluir(agora);

        Assert.Equal(StatusOrdem.CONCLUIDA, ordem.Status);
        Assert.Equal(agora, ordem.DataProcessamento);
        Assert.Null(ordem.DataAgendamento);
    }

    [Fact]
    public void OrdemProcessamento_Rejeitar_DeveSetarStatusEDataProcessamento()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var ordem = new Ordem { Status = StatusOrdem.EM_PROCESSAMENTO };
        ordem.Rejeitar(agora);

        Assert.Equal(StatusOrdem.REJEITADA, ordem.Status);
        Assert.Equal(agora, ordem.DataProcessamento);
    }

    [Fact]
    public void OrdemService_Agendamento_DeveRejeitar_QuandoFimDeSemana()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 0m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var sabado = new DateOnly(2026, 5, 2);
        var ex = Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemAgendada(fundo, posicao: null, TipoOperacao.APORTE, 10m, sabado, agora));
        Assert.Contains("dia útil", ex.Message);
    }

    [Fact]
    public void OrdemService_CriarOrdemAgendadaResgate_DeveRejeitar_QuandoCotasInsuficientes()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 0m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 1m };
        var data = new DateOnly(2026, 5, 4);
        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemAgendada(fundo, posicao, TipoOperacao.RESGATE, 2m, data, agora));
    }

    [Fact]
    public void OrdemProcessamentoService_ProcessarAporte_DeveRejeitar_QuandoFundoFechado()
    {
        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 1000m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.FECHADO
        };

        var ordem = new Ordem { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, TipoOperacao = TipoOperacao.APORTE, QuantidadeCotas = 10m, Status = StatusOrdem.CRIADA, DataCriacao = new DateTime(2026, 5, 1, 10, 0, 0) };
        Assert.Throws<BusinessRuleException>(() => ordem.Processar(cliente, fundo, posicao: null, new DateTime(2026, 5, 1, 10, 0, 0)));
    }
}

public sealed class DomainMoreCoverageTests
{
    [Fact]
    public void Ordem_Processar_DeveRejeitar_QuandoTipoInvalido()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 1000m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordem = new Ordem { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, TipoOperacao = (TipoOperacao)999, QuantidadeCotas = 10m, Status = StatusOrdem.CRIADA, DataCriacao = agora };
        Assert.Throws<BusinessRuleException>(() => ordem.Processar(cliente, fundo, posicao: null, agora));
    }

    [Fact]
    public void OrdemProcessamentoService_ProcessarAporte_DeveRejeitar_QuandoQuantidadeAbaixoDoMinimo()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 1000m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 100m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordem = new Ordem { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, TipoOperacao = TipoOperacao.APORTE, QuantidadeCotas = 10m, Status = StatusOrdem.CRIADA, DataCriacao = agora };
        Assert.Throws<BusinessRuleException>(() => ordem.Processar(cliente, fundo, posicao: null, agora));
    }

    [Fact]
    public void OrdemProcessamentoService_ProcessarAporte_DeveRejeitar_QuandoSaldoInsuficiente()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 50m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordem = new Ordem { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, TipoOperacao = TipoOperacao.APORTE, QuantidadeCotas = 10m, Status = StatusOrdem.CRIADA, DataCriacao = agora };
        Assert.Throws<BusinessRuleException>(() => ordem.Processar(cliente, fundo, posicao: null, agora));
    }

    [Fact]
    public void OrdemProcessamentoService_ProcessarAporte_DeveCriarPosicao_QuandoNaoExiste()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 1000m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordem = new Ordem { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, TipoOperacao = TipoOperacao.APORTE, QuantidadeCotas = 10m, Status = StatusOrdem.CRIADA, DataCriacao = agora };
        ordem.Processar(cliente, fundo, posicao: null, agora);

        Assert.Single(cliente.Posicoes);
        Assert.Single(fundo.Posicoes);
        Assert.Equal(cliente.IdCliente, cliente.Posicoes.Single().IdCliente);
        Assert.Equal(fundo.IdFundo, cliente.Posicoes.Single().IdFundo);
        Assert.Equal(10m, cliente.Posicoes.Single().QuantidadeCotas);
    }

    [Fact]
    public void OrdemProcessamentoService_ProcessarResgate_DeveRejeitar_QuandoTipoInvalido()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 0m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 10m };

        var ordem = new Ordem { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, TipoOperacao = (TipoOperacao)999, QuantidadeCotas = 1m, Status = StatusOrdem.CRIADA, DataCriacao = agora };
        Assert.Throws<BusinessRuleException>(() => ordem.Processar(cliente, fundo, posicao, agora));
    }

    [Fact]
    public void OrdemProcessamentoService_ProcessarResgate_DeveRejeitar_QuandoFundoFechado()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 0m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.FECHADO
        };
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 10m };

        var ordem = new Ordem { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, TipoOperacao = TipoOperacao.RESGATE, QuantidadeCotas = 1m, Status = StatusOrdem.CRIADA, DataCriacao = agora };
        Assert.Throws<BusinessRuleException>(() => ordem.Processar(cliente, fundo, posicao, agora));
    }

    [Fact]
    public void OrdemProcessamentoService_ProcessarResgate_DeveRejeitar_QuandoViolaPermanenciaMinima()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 0m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 50m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 60m };

        var ordem = new Ordem { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, TipoOperacao = TipoOperacao.RESGATE, QuantidadeCotas = 20m, Status = StatusOrdem.CRIADA, DataCriacao = agora };
        Assert.Throws<BusinessRuleException>(() => ordem.Processar(cliente, fundo, posicao, agora));
    }

    [Fact]
    public void OrdemService_CriarOrdemAportePorCotas_DevePreencherCamposBasicos()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = new Cliente { IdCliente = Guid.NewGuid(), Nome = "Cliente", Cpf = "00000000000", SaldoDisponivel = 1000m };
        var fundo = new Fundo
        {
            IdFundo = Guid.NewGuid(),
            Nome = "Fundo",
            HorarioCorte = new TimeSpan(14, 0, 0),
            ValorCota = 10m,
            ValorMinimoAporte = 1m,
            ValorMinimoPermanencia = 0m,
            StatusCaptacao = StatusCaptacao.ABERTO
        };

        var ordem = cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.APORTE, 10m, agora);
        Assert.Equal(cliente.IdCliente, ordem.IdCliente);
        Assert.Equal(fundo.IdFundo, ordem.IdFundo);
        Assert.Equal(TipoOperacao.APORTE, ordem.TipoOperacao);
        Assert.Equal(10m, ordem.QuantidadeCotas);
        Assert.Equal(StatusOrdem.CRIADA, ordem.Status);
        Assert.Equal(agora, ordem.DataCriacao);
        Assert.Null(ordem.DataAgendamento);
    }
}

internal sealed class InMemoryTransactionManager : ITransactionManager
{
    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        return operation(cancellationToken);
    }
}

internal sealed class InMemoryClienteRepository : IClienteRepository
{
    private readonly Dictionary<Guid, Cliente> _items;

    public InMemoryClienteRepository(params Cliente[] clientes)
    {
        _items = clientes.ToDictionary(x => x.IdCliente);
    }

    public Task<Cliente?> GetByIdAsync(Guid idCliente, CancellationToken cancellationToken)
    {
        _items.TryGetValue(idCliente, out var cliente);
        return Task.FromResult<Cliente?>(cliente);
    }
}

internal sealed class InMemoryFundoRepository : IFundoRepository
{
    private readonly Dictionary<Guid, Fundo> _items;

    public InMemoryFundoRepository(params Fundo[] fundos)
    {
        _items = fundos.ToDictionary(x => x.IdFundo);
    }

    public Task<Fundo?> GetByIdAsync(Guid idFundo, CancellationToken cancellationToken)
    {
        _items.TryGetValue(idFundo, out var fundo);
        return Task.FromResult<Fundo?>(fundo);
    }
}

internal sealed class InMemoryPosicaoRepository : IPosicaoRepository
{
    private readonly List<Posicao> _items;

    public InMemoryPosicaoRepository(params Posicao[] posicoes)
    {
        _items = posicoes.ToList();
    }

    public Task<Posicao?> GetByIdAsync(Guid idCliente, Guid idFundo, CancellationToken cancellationToken)
    {
        return Task.FromResult(_items.FirstOrDefault(x => x.IdCliente == idCliente && x.IdFundo == idFundo));
    }

    public Task<IReadOnlyList<Posicao>> ListByClienteIdAsync(Guid idCliente, CancellationToken cancellationToken)
    {
        IReadOnlyList<Posicao> result = _items.Where(x => x.IdCliente == idCliente).ToList();
        return Task.FromResult(result);
    }
}

internal sealed class InMemoryOrdemRepository : IOrdemRepository
{
    private readonly Dictionary<Guid, IdempotencyMetadata> _idempotency = new();

    public List<Ordem> Items { get; }

    public InMemoryOrdemRepository(params Ordem[] ordens)
    {
        Items = ordens.ToList();
    }

    public Task AddAsync(Ordem ordem, IdempotencyMetadata? idempotency, CancellationToken cancellationToken)
    {
        Items.Add(ordem);
        if (idempotency is not null)
        {
            _idempotency[ordem.IdOrdem] = idempotency;
        }
        return Task.CompletedTask;
    }

    public Task<OrdemIdempotencyMatch?> GetByIdempotencyAsync(Guid idCliente, string operation, string key, CancellationToken cancellationToken)
    {
        var found = Items
            .Where(x => x.IdCliente == idCliente && _idempotency.TryGetValue(x.IdOrdem, out var meta) && meta.Operation == operation && meta.Key == key)
            .OrderByDescending(x => x.DataCriacao)
            .FirstOrDefault();

        if (found is null)
        {
            return Task.FromResult<OrdemIdempotencyMatch?>(null);
        }

        _idempotency.TryGetValue(found.IdOrdem, out var idempotency);
        return Task.FromResult<OrdemIdempotencyMatch?>(new OrdemIdempotencyMatch(found, idempotency?.RequestHash));
    }

    public Task<IReadOnlyList<Ordem>> ListByClienteIdAsync(Guid idCliente, CancellationToken cancellationToken)
    {
        IReadOnlyList<Ordem> result = Items.Where(x => x.IdCliente == idCliente).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Ordem>> ListAgendadasParaProcessarAsync(DateTime agora, int maximo, CancellationToken cancellationToken)
    {
        var inicio = agora.Date;
        var fim = inicio.AddDays(1);
        IReadOnlyList<Ordem> result = Items
            .Where(x => x.Status == StatusOrdem.AGENDADA && x.DataAgendamento.HasValue && x.DataAgendamento.Value >= inicio && x.DataAgendamento.Value < fim)
            .OrderBy(x => x.DataAgendamento)
            .Take(maximo)
            .ToList();
        return Task.FromResult(result);
    }

    public IdempotencyMetadata? GetIdempotency(Guid idOrdem)
    {
        return _idempotency.TryGetValue(idOrdem, out var value) ? value : null;
    }
}

public sealed class DomainCriticalRulesTests
{
    [Fact]
    public void OrdemService_CriarOrdemAportePorCotas_DeveRejeitar_QuandoQuantidadeInvalida()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = NovoCliente(1000m);
        var fundo = NovoFundoAberto();

        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.APORTE, 0m, agora));
        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.APORTE, -1m, agora));
    }

    [Fact]
    public void OrdemService_CriarOrdemResgate_DeveRejeitar_QuandoQuantidadeInvalida()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto();
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 10m };

        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao, TipoOperacao.RESGATE, 0m, agora));
        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao, TipoOperacao.RESGATE, -1m, agora));
    }

    [Fact]
    public void OrdemService_CriarOrdemResgate_DeveRejeitar_QuandoPosicaoNula()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto();

        var ex = Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.RESGATE, 1m, agora));
        Assert.Contains("Cotas insuficientes", ex.Message);
    }

    [Fact]
    public void OrdemService_Cutoff_DevePermitir_QuandoAgoraIgualAoHorarioCorte()
    {
        var agora = new DateTime(2026, 5, 1, 14, 0, 0);
        var cliente = NovoCliente(1000m);
        var fundo = NovoFundoAberto(cutoff: new TimeSpan(14, 0, 0));

        var ordem = cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.APORTE, 10m, agora);
        Assert.Equal(StatusOrdem.CRIADA, ordem.Status);
    }

    [Fact]
    public void OrdemService_CriarOrdemAportePorCotas_DeveRejeitar_QuandoForaDoCutoff()
    {
        var agora = new DateTime(2026, 5, 1, 14, 0, 1);
        var cliente = NovoCliente(1000m);
        var fundo = NovoFundoAberto(cutoff: new TimeSpan(14, 0, 0));

        var ex = Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao: null, TipoOperacao.APORTE, 10m, agora));
        Assert.Contains("cut-off", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OrdemService_CriarOrdemResgate_DeveRejeitar_QuandoForaDoCutoff()
    {
        var agora = new DateTime(2026, 5, 1, 14, 0, 1);
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(cutoff: new TimeSpan(14, 0, 0));
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 10m };

        var ex = Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemImediata(fundo, posicao, TipoOperacao.RESGATE, 1m, agora));
        Assert.Contains("cut-off", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OrdemService_CriarOrdemAgendadaAporte_DeveDefinir_StatusEDataAgendamento()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(cutoff: new TimeSpan(14, 0, 0));

        var dataAgendamento = new DateOnly(2026, 5, 4);
        var ordem = cliente.CriarOrdemAgendada(fundo, posicao: null, TipoOperacao.APORTE, 10m, dataAgendamento, agora);

        Assert.Equal(StatusOrdem.AGENDADA, ordem.Status);
        Assert.Equal(new DateTime(2026, 5, 4, 14, 0, 0), ordem.DataAgendamento);
        Assert.Equal(agora, ordem.DataCriacao);
    }

    [Fact]
    public void OrdemService_CriarOrdemAgendadaAporte_DeveRejeitar_QuandoDataNaoEFutura()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto();

        var hoje = DateOnly.FromDateTime(agora.Date);
        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemAgendada(fundo, posicao: null, TipoOperacao.APORTE, 10m, hoje, agora));
    }

    [Fact]
    public void OrdemService_CriarOrdemAgendadaAporte_DeveRejeitar_QuandoQuantidadeAbaixoDoMinimo()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorMinimoAporte: 100m);

        var dataAgendamento = new DateOnly(2026, 5, 4);
        Assert.Throws<BusinessRuleException>(() => cliente.CriarOrdemAgendada(fundo, posicao: null, TipoOperacao.APORTE, 10m, dataAgendamento, agora));
    }

    [Fact]
    public void OrdemProcessamentoService_ProcessarResgate_DeveAtualizarSaldoEPosicao_QuandoValido()
    {
        var agora = new DateTime(2026, 5, 1, 10, 0, 0);

        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoPermanencia: 0m);
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 10m };

        var ordem = new Ordem
        {
            IdOrdem = Guid.NewGuid(),
            IdCliente = cliente.IdCliente,
            IdFundo = fundo.IdFundo,
            TipoOperacao = TipoOperacao.RESGATE,
            QuantidadeCotas = 2m,
            DataCriacao = agora,
            Status = StatusOrdem.CRIADA
        };

        ordem.Processar(cliente, fundo, posicao, agora);

        Assert.Equal(8m, posicao.QuantidadeCotas);
        Assert.Equal(20m, cliente.SaldoDisponivel);
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
        decimal valorMinimoAporte = 1m,
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
}
