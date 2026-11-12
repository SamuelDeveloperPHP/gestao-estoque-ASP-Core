using GestaoEstoque.Modules.Estoque.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestaoEstoque.Modules.Estoque;

/// <summary>Marcador de assembly usado por testes de arquitetura.</summary>
public static class AssemblyMarker;

public static class EstoqueModule
{
    public static IServiceCollection AddEstoqueModule(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Postgres")
                 ?? throw new InvalidOperationException("ConnectionStrings:Postgres não configurada.");

        services.AddDbContext<EstoqueDbContext>(o =>
            o.UseNpgsql(cs, n => n.MigrationsHistoryTable("__ef_migrations", EstoqueDbContext.Schema)));

        return services;
    }
}
