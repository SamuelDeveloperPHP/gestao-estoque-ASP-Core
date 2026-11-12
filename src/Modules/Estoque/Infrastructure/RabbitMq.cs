using System.Text;
using GestaoEstoque.Modules.Estoque.Application;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace GestaoEstoque.Modules.Estoque.Infrastructure;

public sealed class RabbitMqOptions
{
    public const string Secao = "RabbitMq";

    public string Host { get; set; } = "";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "";
    public bool Habilitado => !string.IsNullOrWhiteSpace(Host);
}

/// <summary>Conexão única e preguiçosa, com publisher confirms. Recria a conexão se ela cair.</summary>
public sealed class RabbitMqPublisher(IOptions<RabbitMqOptions> options) : IMensageriaPublisher, IDisposable
{
    private readonly object _trava = new();
    private IConnection? _conexao;
    private IModel? _canal;

    public void Publicar(string rotaChave, Guid messageId, string payloadJson)
    {
        lock (_trava)
        {
            try
            {
                var canal = ObterCanal();
                var props = canal.CreateBasicProperties();
                props.Persistent = true;
                props.ContentType = "application/json";
                props.MessageId = messageId.ToString();

                canal.BasicPublish(RotasEstoque.Exchange, rotaChave, props, Encoding.UTF8.GetBytes(payloadJson));
                canal.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
            }
            catch
            {
                Fechar(); // a próxima tentativa abre uma conexão nova
                throw;
            }
        }
    }

    private IModel ObterCanal()
    {
        if (_canal is { IsOpen: true } && _conexao is { IsOpen: true }) return _canal;

        Fechar();
        var o = options.Value;
        _conexao = new ConnectionFactory
        {
            HostName = o.Host,
            Port = o.Port,
            UserName = o.User,
            Password = o.Password,
            AutomaticRecoveryEnabled = true
        }.CreateConnection("gestao-estoque-outbox");

        _canal = _conexao.CreateModel();
        _canal.ExchangeDeclare(RotasEstoque.Exchange, ExchangeType.Topic, durable: true);
        _canal.ConfirmSelect();
        return _canal;
    }

    private void Fechar()
    {
        try { _canal?.Dispose(); } catch { /* já quebrado */ }
        try { _conexao?.Dispose(); } catch { /* já quebrado */ }
        _canal = null;
        _conexao = null;
    }

    public void Dispose()
    {
        lock (_trava) Fechar();
    }
}
