using CaseGig.Domain.Entities;

namespace CaseGig.Application.Abstractions;

public interface IOrdemRepository
{
    Task AddAsync(Ordem ordem, CancellationToken cancellationToken);
    Task<Ordem?> GetByIdempotencyAsync(Guid idCliente, string operation, string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<Ordem>> ListByClienteIdAsync(Guid idCliente, CancellationToken cancellationToken);
    Task<IReadOnlyList<Ordem>> ListAgendadasParaProcessarAsync(DateTime agora, int maximo, CancellationToken cancellationToken);
}
