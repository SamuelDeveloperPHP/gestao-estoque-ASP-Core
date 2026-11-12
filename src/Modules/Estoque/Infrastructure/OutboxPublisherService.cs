using System.Diagnostics;
using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Estoque.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GestaoEstoque.Modules.Estoque.Infrastructure;

/// <summary>
/// Lê o Outbox e publica no broker. "FOR UPDATE SKIP LOCKED" permite várias réplicas sem publicar em duplicidade
/// no mesmo instante; mesmo assim a entrega é "pelo menos uma vez", então consumidores devem deduplicar por MessageId.
/// </summary>
public sealed class OutboxPublisherService(
    IServiceScopeFactory escopos,
    IMensageriaPublisher publisher,
    ILogger<OutboxPublisherService> logger) : BackgroundService
{
    private const int Lote = 50;
    private static readonly ActivitySource Fonte = new(Observabilidade.FonteOutbox);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var espera = TimeSpan.FromMilliseconds(500);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var publicadas = await PublicarLoteAsync(ct);
                espera = publicadas == 0 ? TimeSpan.FromSeconds(1) : TimeSpan.Zero;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Broker fora do ar: nada se perde (a mensagem continua no banco). Recua e tenta de novo.
                logger.LogWarning(ex, "Falha ao publicar o Outbox; nova tentativa em instantes.");
                espera = TimeSpan.FromSeconds(5);
            }

            if (espera > TimeSpan.Zero) await Task.Delay(espera, ct);
        }
    }

    private async Task<int> PublicarLoteAsync(CancellationToken ct)
    {
        using var escopo = escopos.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<EstoqueDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var pendentes = await db.Outbox
            .FromSqlRaw("""
                SELECT * FROM estoque.outbox
                WHERE "ProcessadoEm" IS NULL
                ORDER BY "CriadoEm"
                LIMIT {0}
                FOR UPDATE SKIP LOCKED
                """, Lote)
            .ToListAsync(ct);

        if (pendentes.Count == 0) return 0;

        using var span = Fonte.StartActivity("outbox.publicar");
        span?.SetTag("outbox.mensagens", pendentes.Count);

        foreach (var m in pendentes)
        {
            publisher.Publicar(m.RotaChave, m.Id, m.Payload);
            m.MarcarProcessada();
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return pendentes.Count;
    }
}
