using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GestaoEstoque.Modules.Identidade.Infrastructure;

/// <summary>Usada só pelas ferramentas do EF (dotnet ef migrations ...); não conecta ao banco.</summary>
internal sealed class IdentidadeDbContextFactory : IDesignTimeDbContextFactory<IdentidadeDbContext>
{
    public IdentidadeDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                 ?? "Host=localhost;Database=gestao_estoque;Username=estoque;Password=design-time";

        var options = new DbContextOptionsBuilder<IdentidadeDbContext>()
            .UseNpgsql(cs, n => n.MigrationsHistoryTable("__ef_migrations", IdentidadeDbContext.Schema))
            .Options;

        return new IdentidadeDbContext(options);
    }
}
