using CaseGig.Application.Abstractions;
using CaseGig.Domain.Entities;
using CaseGig.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CaseGig.Infrastructure.Repositories;

public sealed class FundoRepository : IFundoRepository
{
    private readonly InvestmentDbContext _dbContext;

    public FundoRepository(InvestmentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Fundo?> GetByIdAsync(Guid idFundo, CancellationToken cancellationToken)
    {
        return _dbContext.Fundos.FirstOrDefaultAsync(x => x.IdFundo == idFundo, cancellationToken);
    }
}
