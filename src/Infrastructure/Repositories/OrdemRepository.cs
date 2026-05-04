using CaseGig.Application.Abstractions;
using CaseGig.Domain.Entities;
using CaseGig.Domain.Enums;
using CaseGig.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CaseGig.Infrastructure.Repositories;

public sealed class OrdemRepository : IOrdemRepository
{
    private readonly InvestmentDbContext _dbContext;

    public OrdemRepository(InvestmentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Ordem ordem, IdempotencyMetadata? idempotency, CancellationToken cancellationToken)
    {
        _dbContext.Ordens.Add(ordem);
        if (idempotency is not null)
        {
            var entry = _dbContext.Entry(ordem);
            entry.Property("IdempotencyKey").CurrentValue = idempotency.Key;
            entry.Property("IdempotencyOperation").CurrentValue = idempotency.Operation;
            entry.Property("IdempotencyRequestHash").CurrentValue = idempotency.RequestHash;
        }
        return Task.CompletedTask;
    }

    public async Task<OrdemIdempotencyMatch?> GetByIdempotencyAsync(Guid idCliente, string operation, string key, CancellationToken cancellationToken)
    {
        return await _dbContext.Ordens
            .AsNoTracking()
            .Where(x =>
                x.IdCliente == idCliente
                && EF.Property<string?>(x, "IdempotencyOperation") == operation
                && EF.Property<string?>(x, "IdempotencyKey") == key)
            .OrderByDescending(x => x.DataCriacao)
            .Select(x => new OrdemIdempotencyMatch(x, EF.Property<string?>(x, "IdempotencyRequestHash")))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ordem>> ListByClienteIdAsync(Guid idCliente, CancellationToken cancellationToken)
    {
        return await _dbContext.Ordens
            .Where(x => x.IdCliente == idCliente)
            .OrderByDescending(x => x.DataCriacao)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ordem>> ListAgendadasParaProcessarAsync(DateTime agora, int maximo, CancellationToken cancellationToken)
    {
        return await _dbContext.Ordens
            .Where(x => x.Status == StatusOrdem.AGENDADA && x.DataAgendamento != null && x.DataAgendamento <= agora)
            .OrderBy(x => x.DataAgendamento)
            .ThenBy(x => x.DataCriacao)
            .Take(maximo)
            .ToListAsync(cancellationToken);
    }
}
