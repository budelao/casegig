using CaseGig.Domain.Entities;

namespace CaseGig.Application.Abstractions;

public sealed record IdempotencyMetadata(string Operation, string Key, string RequestHash);

public sealed record OrdemIdempotencyMatch(Ordem Ordem, string? RequestHash);

public interface IOrdemRepository
{
    Task AddAsync(Ordem ordem, IdempotencyMetadata? idempotency, CancellationToken cancellationToken);
    Task<OrdemIdempotencyMatch?> GetByIdempotencyAsync(Guid idCliente, string operation, string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<Ordem>> ListByClienteIdAsync(Guid idCliente, CancellationToken cancellationToken);
    Task<IReadOnlyList<Ordem>> ListAgendadasParaProcessarAsync(DateTime agora, int maximo, CancellationToken cancellationToken);
}
