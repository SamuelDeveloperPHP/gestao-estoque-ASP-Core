namespace GestaoEstoque.Modules.Estoque.Application;

/// <summary>
/// Porta de saída: o Estoque precisa saber se o produto existe, mas não conhece o módulo Catálogo.
/// A implementação é ligada no composition root (Api).
/// </summary>
public interface IVerificadorProduto
{
    Task<bool> ExisteAtivoAsync(Guid produtoId, CancellationToken ct);
}
