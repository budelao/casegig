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

    public Task AddAsync(Ordem ordem, CancellationToken cancellationToken)
    {
        _dbContext.Ordens.Add(ordem);
        return Task.CompletedTask;
    }

    public async Task<Ordem?> GetByIdempotencyAsync(Guid idCliente, string operation, string key, CancellationToken cancellationToken)
    {
        return await _dbContext.Ordens
            .AsNoTracking()
            .Where(x => x.IdCliente == idCliente && x.IdempotencyOperation == operation && x.IdempotencyKey == key)
            .OrderByDescending(x => x.DataCriacao)
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
