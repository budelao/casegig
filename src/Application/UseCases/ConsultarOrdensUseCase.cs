using CaseGig.Application.Abstractions;
using CaseGig.Application.DTOs;

namespace CaseGig.Application.UseCases;

public sealed class ConsultarOrdensUseCase
{
    private readonly IOrdemRepository _ordemRepository;

    public ConsultarOrdensUseCase(IOrdemRepository ordemRepository)
    {
        _ordemRepository = ordemRepository;
    }

    public async Task<IReadOnlyList<OrdemDto>> ExecuteAsync(Guid idCliente, CancellationToken cancellationToken)
    {
        var ordens = await _ordemRepository.ListByClienteIdAsync(idCliente, cancellationToken);
        return ordens
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
    }
}
