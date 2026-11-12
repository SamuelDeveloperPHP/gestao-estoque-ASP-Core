using GestaoEstoque.Modules.Catalogo.Application;
using GestaoEstoque.Modules.Catalogo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestaoEstoque.Modules.Catalogo;

public static class AssemblyMarker;

public static class CatalogoModule
{
    public static IServiceCollection AddCatalogoModule(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Postgres")
                 ?? throw new InvalidOperationException("ConnectionStrings:Postgres não configurada.");

        services.AddDbContext<CatalogoDbContext>(o =>
            o.UseNpgsql(cs, n => n.MigrationsHistoryTable("__ef_migrations", CatalogoDbContext.Schema)));

        services.AddScoped<ProdutoService>();
        services.AddScoped<IConsultaProdutos>(sp => sp.GetRequiredService<ProdutoService>());

        return services;
    }

    public static async Task MigrarCatalogoAsync(this IServiceProvider provider, CancellationToken ct = default)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CatalogoDbContext>().Database.MigrateAsync(ct);
    }
}
