using CaseGig.Application.Abstractions;
using CaseGig.Application.Exceptions;
using CaseGig.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CaseGig.Infrastructure.Transactions;

public sealed class EfTransactionManager : ITransactionManager
{
    private readonly InvestmentDbContext _dbContext;

    public EfTransactionManager(InvestmentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await operation(cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new ConcurrencyException("Conflito de concorrência ao persistir dados.", ex);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
