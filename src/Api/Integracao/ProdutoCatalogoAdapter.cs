using GestaoEstoque.Modules.Catalogo.Application;
using GestaoEstoque.Modules.Estoque.Application;

namespace GestaoEstoque.Api.Integracao;

/// <summary>
/// Cola entre módulos no composition root: Estoque (porta) ← Catálogo (contrato público).
/// É o único lugar que conhece os dois lados, preservando o isolamento entre eles.
/// </summary>
internal sealed class ProdutoCatalogoAdapter(IConsultaProdutos catalogo) : IVerificadorProduto
{
    public Task<bool> ExisteAtivoAsync(Guid produtoId, CancellationToken ct) =>
        catalogo.ExisteAtivoAsync(produtoId, ct);
}
