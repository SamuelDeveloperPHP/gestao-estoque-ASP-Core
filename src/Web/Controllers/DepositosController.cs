using GestaoEstoque.Web.Models;
using GestaoEstoque.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoEstoque.Web.Controllers;

public sealed class DepositosController(ApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var resultado = await api.ListarDepositosAsync(apenasAtivos: false, ct);
        return View(new DepositosIndexViewModel
        {
            Itens = resultado.Dados ?? [],
            Erro = resultado.Sucesso ? null : resultado.Erro
        });
    }

    [Authorize(Policy = "Gestao"), HttpGet]
    public IActionResult Novo() => View(new NovoDepositoViewModel());

    [Authorize(Policy = "Gestao"), HttpPost]
    public async Task<IActionResult> Novo(NovoDepositoViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var resultado = await api.CriarDepositoAsync(model.Codigo, model.Nome, ct);
        if (!resultado.Sucesso)
        {
            ModelState.AddModelError("", resultado.Erro ?? "Não foi possível criar o depósito.");
            return View(model);
        }

        TempData["ok"] = $"Depósito {resultado.Dados?.Codigo} cadastrado.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "Gestao"), HttpPost]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        var resultado = await api.DesativarDepositoAsync(id, ct);
        if (resultado.Sucesso) TempData["ok"] = "Depósito desativado.";
        else TempData["erro"] = resultado.Erro;
        return RedirectToAction(nameof(Index));
    }
}
