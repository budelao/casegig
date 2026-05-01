using CaseGig.Application.Abstractions;
using CaseGig.Infrastructure.Persistence;
using CaseGig.Infrastructure.Repositories;
using CaseGig.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace CaseGig.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MySql");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:MySql não configurado.");
        }

        services.AddDbContext<InvestmentDbContext>(options =>
        {
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
            options.UseMySql(connectionString, serverVersion);
        });

        services.AddScoped<ITransactionManager, EfTransactionManager>();
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IFundoRepository, FundoRepository>();
        services.AddScoped<IPosicaoRepository, PosicaoRepository>();
        services.AddScoped<IOrdemRepository, OrdemRepository>();

        return services;
    }
}
