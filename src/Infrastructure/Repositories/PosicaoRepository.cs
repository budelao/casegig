using CaseGig.Application.Abstractions;
using CaseGig.Domain.Entities;
using CaseGig.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CaseGig.Infrastructure.Repositories;

public sealed class PosicaoRepository : IPosicaoRepository
{
    private readonly InvestmentDbContext _dbContext;

    public PosicaoRepository(InvestmentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Posicao?> GetByIdAsync(Guid idCliente, Guid idFundo, CancellationToken cancellationToken)
    {
        return _dbContext.Posicoes.FirstOrDefaultAsync(x => x.IdCliente == idCliente && x.IdFundo == idFundo, cancellationToken);
    }

    public async Task<IReadOnlyList<Posicao>> ListByClienteIdAsync(Guid idCliente, CancellationToken cancellationToken)
    {
        return await _dbContext.Posicoes
            .Where(x => x.IdCliente == idCliente)
            .OrderBy(x => x.IdFundo)
            .ToListAsync(cancellationToken);
    }
}
