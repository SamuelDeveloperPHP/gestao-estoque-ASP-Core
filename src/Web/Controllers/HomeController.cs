using System.Diagnostics;
using GestaoEstoque.Web.Models;
using GestaoEstoque.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoEstoque.Web.Controllers;

public sealed class HomeController(ApiClient api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var modelo = new HomeViewModel
        {
            Nome = User.Identity?.Name ?? "",
            Perfil = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? ""
        };

        var produtos = await api.ListarProdutosAsync(null, 0, 1, ct);
        var saldos = await api.ListarSaldosAsync(null, 0, 1, ct);

        if (produtos.Sucesso) modelo.TotalProdutos = produtos.Total ?? produtos.Dados?.Count;
        if (saldos.Sucesso) modelo.TotalSaldos = saldos.Total ?? saldos.Dados?.Count;
        modelo.Erro = produtos.Erro ?? saldos.Erro;

        return View(modelo);
    }

    [AllowAnonymous]
    public IActionResult Erro() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}

public sealed class ErrorViewModel
{
    public string? RequestId { get; set; }
}
