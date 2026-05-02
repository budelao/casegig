using CaseGig.Application.Abstractions;
using CaseGig.Application.Exceptions;
using CaseGig.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

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
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new ConcurrencyException("Conflito ao persistir dados (violação de unicidade). Tente novamente.", ex);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var dbException = GetInnermostDbException(ex);
        if (dbException is null)
        {
            return false;
        }

        var typeName = dbException.GetType().FullName ?? dbException.GetType().Name;

        if (typeName.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            var number = GetIntProperty(dbException, "Number");
            return number == 1062;
        }

        if (typeName.Contains("SqlException", StringComparison.OrdinalIgnoreCase))
        {
            var number = GetIntProperty(dbException, "Number");
            return number is 2627 or 2601;
        }

        if (typeName.Contains("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            var sqlState = GetStringProperty(dbException, "SqlState");
            return string.Equals(sqlState, "23505", StringComparison.Ordinal);
        }

        var message = dbException.Message ?? string.Empty;
        return message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
               || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
               || message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }

    private static DbException? GetInnermostDbException(Exception ex)
    {
        Exception? current = ex;
        while (current is not null)
        {
            if (current is DbException db)
            {
                return db;
            }

            current = current.InnerException;
        }

        return null;
    }

    private static int? GetIntProperty(object instance, string propertyName)
    {
        var prop = instance.GetType().GetProperty(propertyName);
        if (prop is null || prop.PropertyType != typeof(int))
        {
            return null;
        }

        return (int?)prop.GetValue(instance);
    }

    private static string? GetStringProperty(object instance, string propertyName)
    {
        var prop = instance.GetType().GetProperty(propertyName);
        if (prop is null || prop.PropertyType != typeof(string))
        {
            return null;
        }

        return (string?)prop.GetValue(instance);
    }
}
