using System.Text.Json;

namespace GestaoEstoque.Modules.Estoque.Domain;

/// <summary>
/// Mensagem a publicar no broker. É gravada na MESMA transação da movimentação (padrão Outbox),
/// então "gravou no banco" e "vai ser publicado" nunca divergem. Entrega: pelo menos uma vez.
/// </summary>
public sealed class MensagemOutbox
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private MensagemOutbox() { } // EF Core

    public static MensagemOutbox De(string rotaChave, object evento) => new()
    {
        Id = Guid.NewGuid(),
        RotaChave = rotaChave,
        Payload = JsonSerializer.Serialize(evento, evento.GetType(), Json),
        CriadoEm = DateTime.UtcNow
    };

    public Guid Id { get; private set; }
    public string RotaChave { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTime CriadoEm { get; private set; }
    public DateTime? ProcessadoEm { get; private set; }

    public void MarcarProcessada() => ProcessadoEm = DateTime.UtcNow;
}
