using CaseGig.Application.Abstractions;
using CaseGig.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace CaseGig.Application.UseCases;

public sealed class ConsultarOrdensUseCase
{
    private readonly ILogger<ConsultarOrdensUseCase> _logger;
    private readonly IOrdemRepository _ordemRepository;

    public ConsultarOrdensUseCase(ILogger<ConsultarOrdensUseCase> logger, IOrdemRepository ordemRepository)
    {
        _logger = logger;
        _ordemRepository = ordemRepository;
    }

    public async Task<IReadOnlyList<OrdemDto>> ExecuteAsync(Guid idCliente, CancellationToken cancellationToken)
    {
        _logger.LogInformation("API: Executando caso de uso: consultar ordens. Cliente={IdCliente}", idCliente);
        var ordens = await _ordemRepository.ListByClienteIdAsync(idCliente, cancellationToken);
        var result = ordens
            .Select(o => new OrdemDto(
                o.IdOrdem,
                o.IdCliente,
                o.IdFundo,
                o.TipoOperacao,
                o.Status,
                o.QuantidadeCotas,
                o.DataCriacao,
                o.DataAgendamento,
                o.DataProcessamento))
            .ToList();
        _logger.LogInformation("API: Consulta de ordens concluída. Cliente={IdCliente} Quantidade={Quantidade}", idCliente, result.Count);
        return result;
    }
}
