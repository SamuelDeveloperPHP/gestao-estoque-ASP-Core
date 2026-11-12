using GestaoEstoque.Modules.Estoque.Application;
using GestaoEstoque.Modules.Estoque.Infrastructure;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;

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

        services.AddScoped<RegistrarMovimentacaoHandler>();

        // Enums como texto na API ("Entrada", "Saida").
        services.Configure<JsonOptions>(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return services;
    }

    public static async Task MigrarEstoqueAsync(this IServiceProvider provider, CancellationToken ct = default)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EstoqueDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
