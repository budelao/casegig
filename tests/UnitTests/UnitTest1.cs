using CaseGig.Application.Abstractions;
using CaseGig.Application.DTOs;
using CaseGig.Application.Exceptions;
using CaseGig.Application.UseCases;
using Microsoft.Extensions.Logging.Abstractions;
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

        Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemAgendadaAporte(cliente, fundo, 10m, DateOnly.FromDateTime(HojeAs(0, 0)), HojeAs(10, 0)));
        Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemAgendadaAporte(cliente, fundo, 10m, new DateOnly(2026, 5, 2), HojeAs(10, 0)));
    }

    [Fact]
    public void CriarOrdemAgendadaAporte_NaoDeveValidar_SaldoNoMomentoDoAgendamento()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoAporte: 100m);

        var ordem = _ordemService.CriarOrdemAgendadaAporte(cliente, fundo, 100m, new DateOnly(2026, 5, 4), HojeAs(10, 0));
        Assert.Equal(StatusOrdem.AGENDADA, ordem.Status);
        Assert.Equal(new DateTime(2026, 5, 4, 14, 0, 0), ordem.DataAgendamento);
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
    public void CriarOrdemResgate_DeveRejeitar_QuandoSaldoRemanescenteMaiorQueZeroEFicaAbaixoDoMinimo()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoPermanencia: 50m);
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 200m };

        Assert.Throws<BusinessRuleException>(() => _ordemService.CriarOrdemResgate(cliente, fundo, posicao, 170m, HojeAs(10, 0)));
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

    [Fact]
    public void ProcessarOrdemResgate_DeveDebitar_ExatamenteQuantidadeInformada()
    {
        var cliente = NovoCliente(0m);
        var fundo = NovoFundoAberto(valorCota: 10m, valorMinimoPermanencia: 0m);
        var posicao = new Posicao { IdCliente = cliente.IdCliente, IdFundo = fundo.IdFundo, QuantidadeCotas = 600m };

        var ordem = _ordemService.CriarOrdemResgate(cliente, fundo, posicao, 500m, HojeAs(10, 0));
        _processamentoService.PrepararParaProcessamento(ordem, HojeAs(10, 0));
        _processamentoService.ProcessarOrdemResgate(ordem, cliente, fundo, posicao);
        _processamentoService.Concluir(ordem, HojeAs(10, 0));

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
            new OrdemService(),
            new OrdemProcessamentoService());

        var request = new CriarOrdemRequestDto(cliente.IdCliente, fundo.IdFundo, TipoOperacao.APORTE, 10m);

        var first = await useCase.ExecuteAsync(request, agora, "  key-1  ", CancellationToken.None);
        var second = await useCase.ExecuteAsync(request, agora, "key-1", CancellationToken.None);

        Assert.False(first.IsReplay);
        Assert.True(second.IsReplay);
        Assert.Equal(first.Result.IdOrdem, second.Result.IdOrdem);
        Assert.Single(ordens.Items);
        Assert.Equal("key-1", ordens.Items[0].IdempotencyKey);
        Assert.Equal("POST /ordens", ordens.Items[0].IdempotencyOperation);
        Assert.False(string.IsNullOrWhiteSpace(ordens.Items[0].IdempotencyRequestHash));
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
            new OrdemService(),
            new OrdemProcessamentoService());

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
            new OrdemService(),
            new OrdemProcessamentoService());

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
            new OrdemService());

        var request = new CriarOrdemAgendamentoRequestDto(cliente.IdCliente, fundo.IdFundo, TipoOperacao.APORTE, 10m, new DateOnly(2026, 5, 5));

        var first = await useCase.ExecuteAsync(request, agora, "key-3", CancellationToken.None);
        var second = await useCase.ExecuteAsync(request, agora, "key-3", CancellationToken.None);

        Assert.False(first.IsReplay);
        Assert.True(second.IsReplay);
        Assert.Equal(first.Result.IdOrdem, second.Result.IdOrdem);
        Assert.Single(ordens.Items);
        Assert.Equal("POST /ordens/agendamento", ordens.Items[0].IdempotencyOperation);
    }

    private sealed class InMemoryTransactionManager : ITransactionManager
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
        {
            return operation(cancellationToken);
        }
    }

    private sealed class InMemoryClienteRepository : IClienteRepository
    {
        private readonly Cliente _cliente;

        public InMemoryClienteRepository(Cliente cliente)
        {
            _cliente = cliente;
        }

        public Task<Cliente?> GetByIdAsync(Guid idCliente, CancellationToken cancellationToken)
        {
            return Task.FromResult<Cliente?>(_cliente.IdCliente == idCliente ? _cliente : null);
        }
    }

    private sealed class InMemoryFundoRepository : IFundoRepository
    {
        private readonly Fundo _fundo;

        public InMemoryFundoRepository(Fundo fundo)
        {
            _fundo = fundo;
        }

        public Task<Fundo?> GetByIdAsync(Guid idFundo, CancellationToken cancellationToken)
        {
            return Task.FromResult<Fundo?>(_fundo.IdFundo == idFundo ? _fundo : null);
        }
    }

    private sealed class InMemoryPosicaoRepository : IPosicaoRepository
    {
        private readonly List<Posicao> _items = new();

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

    private sealed class InMemoryOrdemRepository : IOrdemRepository
    {
        public List<Ordem> Items { get; } = new();

        public Task AddAsync(Ordem ordem, CancellationToken cancellationToken)
        {
            Items.Add(ordem);
            return Task.CompletedTask;
        }

        public Task<Ordem?> GetByIdempotencyAsync(Guid idCliente, string operation, string key, CancellationToken cancellationToken)
        {
            var found = Items
                .Where(x => x.IdCliente == idCliente && x.IdempotencyOperation == operation && x.IdempotencyKey == key)
                .OrderByDescending(x => x.DataCriacao)
                .FirstOrDefault();
            return Task.FromResult(found);
        }

        public Task<IReadOnlyList<Ordem>> ListByClienteIdAsync(Guid idCliente, CancellationToken cancellationToken)
        {
            IReadOnlyList<Ordem> result = Items.Where(x => x.IdCliente == idCliente).ToList();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Ordem>> ListAgendadasParaProcessarAsync(DateTime agora, int maximo, CancellationToken cancellationToken)
        {
            IReadOnlyList<Ordem> result = Items
                .Where(x => x.Status == StatusOrdem.AGENDADA && x.DataAgendamento.HasValue && x.DataAgendamento.Value <= agora)
                .OrderBy(x => x.DataAgendamento)
                .Take(maximo)
                .ToList();
            return Task.FromResult(result);
        }
    }
}
