using GestaoEstoque.Modules.Estoque.Domain;

namespace GestaoEstoque.Modules.Estoque.Application;

/// <summary>Evento de integração publicado para outros sistemas/módulos (contrato estável).</summary>
public sealed record MovimentacaoRegistradaIntegracao(
    Guid MovimentacaoId,
    Guid ProdutoId,
    Guid DepositoId,
    TipoMovimentacao Tipo,
    decimal Quantidade,
    decimal SaldoApos,
    Guid? UsuarioId,
    DateTime OcorridoEm);

public static class RotasEstoque
{
    public const string Exchange = "estoque.eventos";
    public const string MovimentacaoRegistrada = "estoque.movimentacao.registrada";
}

/// <summary>Porta de saída para o broker. Implementada na infraestrutura (RabbitMQ).</summary>
public interface IMensageriaPublisher
{
    /// <summary>Publica e só retorna após a confirmação do broker; lança exceção se falhar.</summary>
    void Publicar(string rotaChave, Guid messageId, string payloadJson);
}
