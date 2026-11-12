using GestaoEstoque.Web.Models;
using GestaoEstoque.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestaoEstoque.Web.Controllers;

public sealed class EstoqueController(ApiClient api) : Controller
{
    private const int Limite = 200;

    public async Task<IActionResult> Index(Guid? produtoId, CancellationToken ct)
    {
        var produtos = await api.ListarProdutosAsync(null, 0, Limite, ct);
        var depositos = await api.ListarDepositosAsync(apenasAtivos: false, ct);
        var saldos = await api.ListarSaldosAsync(produtoId, 0, Limite, ct);

        var produtoPorId = (produtos.Dados ?? []).ToDictionary(p => p.Id);
        var depositoPorId = (depositos.Dados ?? []).ToDictionary(d => d.Id);

        var linhas = (saldos.Dados ?? []).Select(s =>
        {
            produtoPorId.TryGetValue(s.ProdutoId, out var p);
            depositoPorId.TryGetValue(s.DepositoId, out var d);
            return new SaldoLinha
            {
                Produto = p?.Nome ?? s.ProdutoId.ToString("N")[..8],
                Sku = p?.Sku ?? "—",
                Deposito = d?.Nome ?? s.DepositoId.ToString("N")[..8],
                Quantidade = s.Quantidade
            };
        }).OrderBy(l => l.Produto).ThenBy(l => l.Deposito).ToList();

        return View(new EstoqueIndexViewModel
        {
            Linhas = linhas,
            ProdutoId = produtoId,
            Produtos = ItensProdutos(produtos.Dados),
            Erro = saldos.Erro ?? produtos.Erro ?? depositos.Erro
        });
    }

    [HttpGet]
    public async Task<IActionResult> Movimentar(Guid? produtoId, CancellationToken ct)
    {
        var modelo = new MovimentarViewModel { ProdutoId = produtoId };
        await CarregarListasAsync(modelo, ct);
        modelo.DepositoId = modelo.Depositos.Count == 1 ? Guid.Parse(modelo.Depositos[0].Value) : null;
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
        var depositos = await api.ListarDepositosAsync(apenasAtivos: true, ct);

        modelo.Produtos = ItensProdutos(produtos.Dados?.Where(p => p.Ativo));
        modelo.Depositos = (depositos.Dados ?? [])
            .Select(d => new SelectListItem($"{d.Codigo} — {d.Nome}", d.Id.ToString())).ToList();

        if (!produtos.Sucesso) ModelState.AddModelError("", produtos.Erro ?? "Não foi possível carregar os produtos.");
        if (!depositos.Sucesso) ModelState.AddModelError("", depositos.Erro ?? "Não foi possível carregar os depósitos.");
    }

    private static List<SelectListItem> ItensProdutos(IEnumerable<ProdutoDto>? produtos) =>
        (produtos ?? []).Select(p => new SelectListItem($"{p.Sku} — {p.Nome}", p.Id.ToString())).ToList();
}
