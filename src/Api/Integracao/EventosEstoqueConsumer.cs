using System.Text;
using GestaoEstoque.Modules.Estoque.Application;
using GestaoEstoque.Modules.Estoque.Infrastructure;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GestaoEstoque.Api.Integracao;

/// <summary>
/// Consumidor de exemplo: registra em log cada evento de estoque. É o ponto de extensão para alertas,
/// relatórios e integrações. Entrega "pelo menos uma vez": consumidores reais devem deduplicar por MessageId.
/// </summary>
public sealed class EventosEstoqueConsumer(
    IOptions<RabbitMqOptions> options,
    ILogger<EventosEstoqueConsumer> logger) : BackgroundService
{
    private const string Fila = "api.estoque.auditoria";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConsumirAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Consumidor do RabbitMQ indisponível; nova tentativa em 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private async Task ConsumirAsync(CancellationToken ct)
    {
        var o = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = o.Host, Port = o.Port, UserName = o.User, Password = o.Password,
            DispatchConsumersAsync = true, AutomaticRecoveryEnabled = true
        };

        using var conexao = factory.CreateConnection("gestao-estoque-consumidor");
        using var canal = conexao.CreateModel();

        canal.ExchangeDeclare(RotasEstoque.Exchange, ExchangeType.Topic, durable: true);
        canal.QueueDeclare(Fila, durable: true, exclusive: false, autoDelete: false);
        canal.QueueBind(Fila, RotasEstoque.Exchange, "estoque.#");
        canal.BasicQos(0, 20, false);

        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.Received += (_, ea) =>
        {
            logger.LogInformation("Evento {Rota} {MessageId}: {Corpo}",
                ea.RoutingKey, ea.BasicProperties.MessageId, Encoding.UTF8.GetString(ea.Body.Span));
            canal.BasicAck(ea.DeliveryTag, multiple: false);
            return Task.CompletedTask;
        };
        canal.BasicConsume(Fila, autoAck: false, consumidor);

        logger.LogInformation("Consumidor de eventos de estoque ativo na fila {Fila}.", Fila);
        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { /* encerrando */ }
    }
}
