using System.Security.Claims;
using GestaoEstoque.Web.Models;
using GestaoEstoque.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GestaoEstoque.Web.Controllers;

public sealed class AccountController(ApiClient api) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null, bool expirada = false)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl, SessaoExpirada = expirada });
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var login = await api.LoginAsync(model.Email, model.Senha, ip, ct);
        if (!login.Sucesso || login.Dados is null)
        {
            ModelState.AddModelError("", login.Erro ?? "Não foi possível entrar.");
            model.Senha = "";
            return View(model);
        }

        var perfil = await api.PerfilAsync(login.Dados.AccessToken, ct);
        if (!perfil.Sucesso || perfil.Dados is null)
        {
            ModelState.AddModelError("", perfil.Erro ?? "Não foi possível carregar o perfil.");
            return View(model);
        }

        var dados = perfil.Dados;
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, dados.Id ?? ""),
                new Claim(ClaimTypes.Name, dados.Nome ?? dados.Email ?? ""),
                new Claim(ClaimTypes.Email, dados.Email ?? ""),
                new Claim(ClaimTypes.Role, dados.Perfil ?? "")
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        var props = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false,
            ExpiresUtc = login.Dados.ExpiraEm // o cookie vale tanto quanto o JWT
        };
        props.StoreTokens([new AuthenticationToken { Name = BearerTokenHandler.NomeToken, Value = login.Dados.AccessToken }]);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), props);

        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl)
            : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AcessoNegado() => View();
}
