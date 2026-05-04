using CaseGig.Domain.Entities;
using CaseGig.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CaseGig.Infrastructure.Persistence;

public sealed class InvestmentDbContext : DbContext
{
    public InvestmentDbContext(DbContextOptions<InvestmentDbContext> options) : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Fundo> Fundos => Set<Fundo>();
    public DbSet<Ordem> Ordens => Set<Ordem>();
    public DbSet<Posicao> Posicoes => Set<Posicao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(x => x.IdCliente);
            entity.Property(x => x.Nome).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Cpf).IsRequired().HasMaxLength(11);
            entity.Property(x => x.SaldoDisponivel).HasPrecision(18, 2);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<Fundo>(entity =>
        {
            entity.HasKey(x => x.IdFundo);
            entity.Property(x => x.Nome).IsRequired().HasMaxLength(200);
            entity.Property(x => x.HorarioCorte).HasColumnType("time(0)");
            entity.Property(x => x.ValorCota).HasPrecision(18, 6);
            entity.Property(x => x.ValorMinimoAporte).HasPrecision(18, 2);
            entity.Property(x => x.ValorMinimoPermanencia).HasPrecision(18, 2);
            entity.Property(x => x.StatusCaptacao).HasConversion<string>().HasMaxLength(10);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<Posicao>(entity =>
        {
            entity.HasKey(x => new { x.IdCliente, x.IdFundo });
            entity.Property(x => x.QuantidadeCotas).HasPrecision(38, 18);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();

            entity.HasOne(x => x.Cliente)
                .WithMany(x => x.Posicoes)
                .HasForeignKey(x => x.IdCliente);

            entity.HasOne(x => x.Fundo)
                .WithMany(x => x.Posicoes)
                .HasForeignKey(x => x.IdFundo);
        });

        modelBuilder.Entity<Ordem>(entity =>
        {
            entity.HasKey(x => x.IdOrdem);
            entity.Property(x => x.TipoOperacao).HasConversion<string>().HasMaxLength(10);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.QuantidadeCotas).HasPrecision(38, 18);
            entity.Property(x => x.DataCriacao).IsRequired();
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.Property<string?>("IdempotencyKey").HasMaxLength(200);
            entity.Property<string?>("IdempotencyOperation").HasMaxLength(80);
            entity.Property<string?>("IdempotencyRequestHash").HasMaxLength(64);
            entity.HasIndex("IdCliente", "IdempotencyOperation", "IdempotencyKey").IsUnique();

            entity.HasOne(x => x.Cliente)
                .WithMany(x => x.Ordens)
                .HasForeignKey(x => x.IdCliente);

            entity.HasOne(x => x.Fundo)
                .WithMany(x => x.Ordens)
                .HasForeignKey(x => x.IdFundo);
        });

        SeedData(modelBuilder);
    }

    public override int SaveChanges()
    {
        IncrementRowVersions();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        IncrementRowVersions();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void IncrementRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            var rowVersionProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "RowVersion");
            if (rowVersionProperty is null)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                rowVersionProperty.CurrentValue = 1L;
                rowVersionProperty.IsModified = true;
                continue;
            }

            if (entry.State == EntityState.Modified)
            {
                var current = rowVersionProperty.CurrentValue is long l ? l : 0L;
                rowVersionProperty.CurrentValue = current + 1L;
                rowVersionProperty.IsModified = true;
            }
        }
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        var cliente1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var cliente2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var fundo1Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var fundo2Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        modelBuilder.Entity<Cliente>().HasData(
            new Cliente
            {
                IdCliente = cliente1Id,
                Nome = "João Silva",
                Cpf = "11111111111",
                SaldoDisponivel = 10000.00m,
                RowVersion = 1
            },
            new Cliente
            {
                IdCliente = cliente2Id,
                Nome = "Maria Souza",
                Cpf = "22222222222",
                SaldoDisponivel = 100.00m,
                RowVersion = 1
            });

        modelBuilder.Entity<Fundo>().HasData(
            new Fundo
            {
                IdFundo = fundo1Id,
                Nome = "Fundo Renda Fixa",
                HorarioCorte = new TimeSpan(14, 0, 0),
                ValorCota = 10.00m,
                ValorMinimoAporte = 100.00m,
                ValorMinimoPermanencia = 50.00m,
                StatusCaptacao = StatusCaptacao.ABERTO,
                RowVersion = 1
            },
            new Fundo
            {
                IdFundo = fundo2Id,
                Nome = "Fundo Ações Fechado",
                HorarioCorte = new TimeSpan(14, 0, 0),
                ValorCota = 20.00m,
                ValorMinimoAporte = 200.00m,
                ValorMinimoPermanencia = 100.00m,
                StatusCaptacao = StatusCaptacao.FECHADO,
                RowVersion = 1
            });

        modelBuilder.Entity<Posicao>().HasData(
            new Posicao
            {
                IdCliente = cliente1Id,
                IdFundo = fundo1Id,
                QuantidadeCotas = 500m,
                RowVersion = 1
            },
            new Posicao
            {
                IdCliente = cliente2Id,
                IdFundo = fundo1Id,
                QuantidadeCotas = 5m,
                RowVersion = 1
            });
    }
}
