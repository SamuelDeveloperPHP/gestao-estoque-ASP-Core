using GestaoEstoque.Web.Models;
using GestaoEstoque.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoEstoque.Web.Controllers;

public sealed class ProdutosController(ApiClient api) : Controller
{
    private const int PorPagina = 20;

    public async Task<IActionResult> Index(string? busca, int pagina = 1, CancellationToken ct = default)
    {
        pagina = Math.Max(pagina, 1);

        // Pede uma linha a mais para saber se existe próxima página, sem precisar de um total.
        var resultado = await api.ListarProdutosAsync(busca, (pagina - 1) * PorPagina, PorPagina + 1, ct);
        var itens = resultado.Dados ?? [];

        return View(new ProdutosIndexViewModel
        {
            Itens = itens.Take(PorPagina).ToList(),
            TemProxima = itens.Count > PorPagina,
            Busca = busca,
            Pagina = pagina,
            Erro = resultado.Sucesso ? null : resultado.Erro
        });
    }

    [Authorize(Policy = "Gestao"), HttpGet]
    public IActionResult Novo() => View(new NovoProdutoViewModel());

    [Authorize(Policy = "Gestao"), HttpPost]
    public async Task<IActionResult> Novo(NovoProdutoViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var resultado = await api.CriarProdutoAsync(model.Sku, model.Nome, model.Unidade, ct);
        if (!resultado.Sucesso)
        {
            ModelState.AddModelError("", resultado.Erro ?? "Não foi possível criar o produto.");
            return View(model);
        }

        TempData["ok"] = $"Produto {resultado.Dados?.Sku} cadastrado.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "Gestao"), HttpPost]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var resultado = await api.DesativarProdutoAsync(id, ct);
        if (resultado.Sucesso) TempData["ok"] = "Produto desativado.";
        else TempData["erro"] = resultado.Erro;
        return RedirectToAction(nameof(Index));
    }
}
