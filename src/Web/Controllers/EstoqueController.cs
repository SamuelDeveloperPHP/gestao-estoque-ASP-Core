using GestaoEstoque.Web.Models;
using GestaoEstoque.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace GestaoEstoque.Web.Controllers;

public sealed class EstoqueController(ApiClient api, IOptions<List<Deposito>> depositosOpcoes) : Controller
{
    private const int Limite = 200;
    private List<Deposito> Depositos => depositosOpcoes.Value;

    public async Task<IActionResult> Index(Guid? produtoId, CancellationToken ct)
    {
        var produtos = await api.ListarProdutosAsync(null, 0, Limite, ct);
        var saldos = await api.ListarSaldosAsync(produtoId, 0, Limite, ct);

        var porId = (produtos.Dados ?? []).ToDictionary(p => p.Id);
        var linhas = (saldos.Dados ?? []).Select(s =>
        {
            porId.TryGetValue(s.ProdutoId, out var p);
            return new SaldoLinha
            {
                Produto = p?.Nome ?? s.ProdutoId.ToString("N")[..8],
                Sku = p?.Sku ?? "—",
                Deposito = NomeDeposito(s.DepositoId),
                Quantidade = s.Quantidade
            };
        }).OrderBy(l => l.Produto).ToList();

        return View(new EstoqueIndexViewModel
        {
            Linhas = linhas,
            ProdutoId = produtoId,
            Produtos = ItensProdutos(produtos.Dados),
            Erro = saldos.Sucesso ? produtos.Erro : saldos.Erro
        });
    }

    [HttpGet]
    public async Task<IActionResult> Movimentar(Guid? produtoId, CancellationToken ct)
    {
        var modelo = new MovimentarViewModel { ProdutoId = produtoId, DepositoId = Depositos.FirstOrDefault()?.Id };
        await CarregarListasAsync(modelo, ct);
        return View(modelo);
    }

    [HttpPost]
    public async Task<IActionResult> Movimentar(MovimentarViewModel model, CancellationToken ct)
    {
        await CarregarListasAsync(model, ct);
        if (!ModelState.IsValid) return View(model);

        var resultado = await api.MovimentarAsync(
            model.ProdutoId!.Value, model.DepositoId!.Value, model.Tipo, model.Quantidade!.Value, model.ChaveIdempotencia, ct);

        if (!resultado.Sucesso)
        {
            // A chave é mantida: se a falha foi de rede e a API chegou a gravar, o reenvio não duplica.
            ModelState.AddModelError("", resultado.Erro ?? "Não foi possível registrar a movimentação.");
            return View(model);
        }

        TempData["ok"] = $"Movimentação registrada. Novo saldo: {resultado.Dados?.SaldoApos:0.####}";
        return RedirectToAction(nameof(Index), new { produtoId = model.ProdutoId });
    }

    private async Task CarregarListasAsync(MovimentarViewModel modelo, CancellationToken ct)
    {
        var produtos = await api.ListarProdutosAsync(null, 0, Limite, ct);
        modelo.Produtos = ItensProdutos(produtos.Dados?.Where(p => p.Ativo));
        modelo.Depositos = Depositos.Select(d => new SelectListItem(d.Nome, d.Id.ToString())).ToList();
        if (!produtos.Sucesso) ModelState.AddModelError("", produtos.Erro ?? "Não foi possível carregar os produtos.");
    }

    private static List<SelectListItem> ItensProdutos(IEnumerable<ProdutoDto>? produtos) =>
        (produtos ?? []).Select(p => new SelectListItem($"{p.Sku} — {p.Nome}", p.Id.ToString())).ToList();

    private string NomeDeposito(Guid id) =>
        Depositos.FirstOrDefault(d => d.Id == id)?.Nome ?? id.ToString("N")[..8];
}
