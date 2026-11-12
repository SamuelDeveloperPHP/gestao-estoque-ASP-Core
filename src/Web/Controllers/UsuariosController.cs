using GestaoEstoque.Web.Models;
using GestaoEstoque.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoEstoque.Web.Controllers;

[Authorize(Policy = "Administracao")]
public sealed class UsuariosController(ApiClient api) : Controller
{
    [HttpGet]
    public IActionResult Novo() => View(new NovoUsuarioViewModel());

    [HttpPost]
    public async Task<IActionResult> Novo(NovoUsuarioViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var resultado = await api.CriarUsuarioAsync(model.Nome, model.Email, model.Senha, model.Perfil, ct);
        if (!resultado.Sucesso)
        {
            ModelState.AddModelError("", resultado.Erro ?? "Não foi possível criar o usuário.");
            model.Senha = "";
            return View(model);
        }

        TempData["ok"] = $"Usuário {resultado.Dados?.Email} criado como {resultado.Dados?.Perfil}.";
        return RedirectToAction(nameof(Novo));
    }
}
