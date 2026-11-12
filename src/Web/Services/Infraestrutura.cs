using System.Net.Http.Headers;
using GestaoEstoque.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace GestaoEstoque.Web.Services;

public sealed class ApiOptions
{
    public const string Secao = "Api";
    public string BaseUrl { get; set; } = "";
}

public sealed class Deposito
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = "";
}

/// <summary>Anexa o JWT guardado no cookie de login a cada chamada à API.</summary>
public sealed class BearerTokenHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    public const string NomeToken = "access_token";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var contexto = accessor.HttpContext;
        if (contexto is not null && request.Headers.Authorization is null)
        {
            var token = await contexto.GetTokenAsync(CookieAuthenticationDefaults.AuthenticationScheme, NomeToken);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, ct);
    }
}

/// <summary>Se a API devolver 401 numa chamada autenticada, encerra a sessão web e pede novo login.</summary>
public sealed class SessaoExpiradaFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not SessaoExpiradaException) return;

        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var retorno = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
        context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl = retorno, expirada = true });
        context.ExceptionHandled = true;
    }
}
