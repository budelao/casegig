using CaseGig.Application.UseCases;

namespace CaseGig.Api.Workers;

public sealed class OrdemAgendadaWorker : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OrdemAgendadaWorker> _logger;
    private readonly TimeSpan _intervalo;
    private readonly int _batchSize;

    public OrdemAgendadaWorker(IServiceScopeFactory serviceScopeFactory, ILogger<OrdemAgendadaWorker> logger, IConfiguration configuration)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;

        var seconds = configuration.GetValue("Worker:IntervalSeconds", 10);
        _intervalo = TimeSpan.FromSeconds(seconds <= 0 ? 10 : seconds);
        _batchSize = configuration.GetValue("Worker:BatchSize", 20);
        if (_batchSize <= 0)
        {
            _batchSize = 20;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar ordens agendadas");
            }

            await Task.Delay(_intervalo, stoppingToken);
        }
    }

    private async Task ProcessarAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<ProcessarOrdensAgendadasUseCase>();

        var agora = DateTime.Now;
        var resumo = await useCase.ExecuteAsync(agora, _batchSize, stoppingToken);

        if (resumo.Processadas > 0 || resumo.Rejeitadas > 0 || resumo.ConflitosConcorrencia > 0)
        {
            _logger.LogInformation(
                "Worker processamento: Processadas={Processadas} Rejeitadas={Rejeitadas} ConflitosConcorrencia={ConflitosConcorrencia}",
                resumo.Processadas,
                resumo.Rejeitadas,
                resumo.ConflitosConcorrencia);
        }
    }
}
