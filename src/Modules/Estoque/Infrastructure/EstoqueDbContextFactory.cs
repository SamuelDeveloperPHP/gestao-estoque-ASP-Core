using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GestaoEstoque.Modules.Estoque.Infrastructure;

/// <summary>Usada só pelas ferramentas do EF (dotnet ef migrations ...); não conecta ao banco.</summary>
internal sealed class EstoqueDbContextFactory : IDesignTimeDbContextFactory<EstoqueDbContext>
{
    public EstoqueDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                 ?? "Host=localhost;Database=gestao_estoque;Username=estoque;Password=design-time";

        var options = new DbContextOptionsBuilder<EstoqueDbContext>()
            .UseNpgsql(cs, n => n.MigrationsHistoryTable("__ef_migrations", EstoqueDbContext.Schema))
            .Options;

        return new EstoqueDbContext(options);
    }
}
