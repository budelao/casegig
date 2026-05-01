using CaseGig.Application.Abstractions;
using CaseGig.Domain.Entities;
using CaseGig.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CaseGig.Infrastructure.Repositories;

public sealed class ClienteRepository : IClienteRepository
{
    private readonly InvestmentDbContext _dbContext;

    public ClienteRepository(InvestmentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Cliente?> GetByIdAsync(Guid idCliente, CancellationToken cancellationToken)
    {
        return _dbContext.Clientes.FirstOrDefaultAsync(x => x.IdCliente == idCliente, cancellationToken);
    }
}
