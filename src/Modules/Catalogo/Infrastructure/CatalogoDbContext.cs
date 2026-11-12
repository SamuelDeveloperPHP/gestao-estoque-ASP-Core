using GestaoEstoque.Modules.Catalogo.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GestaoEstoque.Modules.Catalogo.Infrastructure;

public sealed class CatalogoDbContext(DbContextOptions<CatalogoDbContext> options) : DbContext(options)
{
    public const string Schema = "catalogo";

    public DbSet<Produto> Produtos => Set<Produto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Produto>(b =>
        {
            b.ToTable("produtos");
            b.HasKey(x => x.Id);
            b.Ignore(x => x.DomainEvents);
            b.Property(x => x.Sku).HasMaxLength(50).IsRequired();
            b.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            b.Property(x => x.Unidade).HasMaxLength(10).IsRequired();
            b.HasIndex(x => x.Sku).IsUnique();
        });
    }
}

/// <summary>Usada só pelas ferramentas do EF (dotnet ef migrations ...); não conecta ao banco.</summary>
internal sealed class CatalogoDbContextFactory : IDesignTimeDbContextFactory<CatalogoDbContext>
{
    public CatalogoDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                 ?? "Host=localhost;Database=gestao_estoque;Username=estoque;Password=design-time";

        var options = new DbContextOptionsBuilder<CatalogoDbContext>()
            .UseNpgsql(cs, n => n.MigrationsHistoryTable("__ef_migrations", CatalogoDbContext.Schema))
            .Options;

        return new CatalogoDbContext(options);
    }
}
