using CaseGig.Application.Abstractions;
using CaseGig.Application.Exceptions;
using CaseGig.Domain.Entities;

namespace CaseGig.Application.Idempotency;

public sealed class IdempotencyService
{
    private readonly IOrdemRepository _ordemRepository;

    public IdempotencyService(IOrdemRepository ordemRepository)
    {
        _ordemRepository = ordemRepository;
    }

    public string? NormalizeKey(string? key)
    {
        var trimmed = key?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    public async Task<Ordem?> TryGetReplayAsync(
        Guid idCliente,
        string operation,
        string normalizedKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await _ordemRepository.GetByIdempotencyAsync(idCliente, operation, normalizedKey, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new ConcurrencyException("Idempotency-Key já utilizada com payload diferente.");
        }

        return existing.Ordem;
    }

    public async Task<Ordem?> TryGetReplayAfterFailureAsync(
        Guid idCliente,
        string operation,
        string normalizedKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await _ordemRepository.GetByIdempotencyAsync(idCliente, operation, normalizedKey, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        return string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal)
            ? existing.Ordem
            : null;
    }
}
