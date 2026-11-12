using GestaoEstoque.Modules.Estoque.Domain;
using Microsoft.EntityFrameworkCore;

namespace GestaoEstoque.Modules.Estoque.Infrastructure;

public sealed class EstoqueDbContext(DbContextOptions<EstoqueDbContext> options) : DbContext(options)
{
    public const string Schema = "estoque";

    public DbSet<SaldoEstoque> Saldos => Set<SaldoEstoque>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<SaldoEstoque>(b =>
        {
            b.ToTable("saldos");
            b.HasKey(x => x.Id);
            b.Ignore(x => x.DomainEvents);
            b.Property(x => x.Quantidade).HasPrecision(18, 4);
            b.HasIndex(x => new { x.ProdutoId, x.DepositoId }).IsUnique();
            b.ToTable(t => t.HasCheckConstraint("ck_saldos_quantidade_nao_negativa", "\"Quantidade\" >= 0"));
            // Concorrência otimista via coluna de sistema xmin do PostgreSQL.
            b.Property<uint>("xmin").IsRowVersion();
        });
    }
}
