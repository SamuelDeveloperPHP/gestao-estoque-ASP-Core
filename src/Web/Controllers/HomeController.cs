using System.Diagnostics;
using GestaoEstoque.Web.Models;
using GestaoEstoque.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoEstoque.Web.Controllers;

public sealed class HomeController(ApiClient api) : Controller
{
    private const int Limite = 200;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var modelo = new HomeViewModel
        {
            Nome = User.Identity?.Name ?? "",
            Perfil = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? ""
        };

        var produtos = await api.ListarProdutosAsync(null, 0, Limite, ct);
        var saldos = await api.ListarSaldosAsync(null, 0, Limite, ct);

        if (produtos.Sucesso && produtos.Dados is not null)
            modelo.TotalProdutos = Formatar(produtos.Dados.Count);
        if (saldos.Sucesso && saldos.Dados is not null)
            modelo.TotalSaldos = Formatar(saldos.Dados.Count);
        modelo.Erro = produtos.Erro ?? saldos.Erro;

        return View(modelo);
    }

    // A API devolve páginas; acima do limite mostramos "200+" em vez de um total inexato.
    private static string Formatar(int n) => n >= Limite ? $"{Limite}+" : n.ToString();

    [AllowAnonymous]
    public IActionResult Erro() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}

public sealed class ErrorViewModel
{
    public string? RequestId { get; set; }
}
