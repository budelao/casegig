using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Globalization;

namespace CaseGig.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static void Initialize(IServiceProvider serviceProvider, bool recreateDb, bool isDevelopment)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
        var dbContext = scope.ServiceProvider.GetRequiredService<InvestmentDbContext>();

        if (recreateDb && isDevelopment)
        {
            logger.LogWarning("Recriando banco de dados por solicitação (--recreate-db)");
            dbContext.Database.EnsureDeleted();
            dbContext.Database.Migrate();
        }
        else if (isDevelopment && HasMigrationsHistoryTable(dbContext))
        {
            try
            {
                dbContext.Database.Migrate();
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Falha ao aplicar migrations no startup");
                throw;
            }
        }

        EnsureRequiredSchema(dbContext);
    }

    private static void EnsureRequiredSchema(InvestmentDbContext dbContext)
    {
        var providerName = dbContext.Database.ProviderName ?? string.Empty;
        if (providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            EnsureMySqlColumnExists(dbContext, "Ordens", "IdempotencyKey");
            EnsureMySqlColumnExists(dbContext, "Ordens", "IdempotencyOperation");
            EnsureMySqlColumnExists(dbContext, "Ordens", "IdempotencyRequestHash");
            return;
        }
    }

    private static bool HasMigrationsHistoryTable(InvestmentDbContext dbContext)
    {
        var providerName = dbContext.Database.ProviderName ?? string.Empty;
        if (providerName.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            return MySqlTableExists(dbContext, "__EFMigrationsHistory");
        }

        return true;
    }

    private static bool MySqlTableExists(InvestmentDbContext dbContext, string tableName)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        try
        {
            if (shouldClose)
            {
                connection.Open();
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName";
            AddParameter(cmd, "@tableName", tableName);
            var count = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            return count > 0;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static void EnsureMySqlColumnExists(InvestmentDbContext dbContext, string tableName, string columnName)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        try
        {
            if (shouldClose)
            {
                connection.Open();
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName";
            AddParameter(cmd, "@tableName", tableName);
            AddParameter(cmd, "@columnName", columnName);
            var count = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (count > 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Banco de dados desatualizado: coluna '{columnName}' não encontrada em '{tableName}'. " +
                $"Atualize o schema (aplicando migrations em um banco controlado por EF) ou recrie o banco usando o script scripts/provision-db.ps1.");
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
