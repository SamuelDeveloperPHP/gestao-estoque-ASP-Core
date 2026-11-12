using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GestaoEstoque.BuildingBlocks;

public static class Observabilidade
{
    /// <summary>Nome da fonte de traces do Outbox (para spans manuais).</summary>
    public const string FonteOutbox = "GestaoEstoque.Outbox";

    /// <summary>
    /// Traces de ASP.NET Core, HttpClient (propaga o contexto Web → API) e Npgsql.
    /// Só exporta se OTEL_EXPORTER_OTLP_ENDPOINT estiver definido (ex.: http://jaeger:4317).
    /// </summary>
    public static IServiceCollection AddObservabilidade(this IServiceCollection services, IConfiguration config, string servico)
    {
        var exportar = !string.IsNullOrWhiteSpace(config["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(servico))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation(o =>
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"));
                t.AddHttpClientInstrumentation();
                t.AddNpgsql();
                t.AddSource(FonteOutbox);
                if (exportar) t.AddOtlpExporter();
            });

        return services;
    }
}
