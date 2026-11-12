using System.Security.Claims;
using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Identidade.Application;
using GestaoEstoque.Modules.Identidade.Domain;
using GestaoEstoque.Modules.Identidade.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GestaoEstoque.Modules.Identidade.Api;

public sealed record LoginRequest(string? Email, string? Senha);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiraEm, string Perfil);

public sealed record CriarUsuarioRequest(string? Nome, string? Email, string? Senha, Perfil Perfil);

public sealed record UsuarioResponse(Guid Id, string Nome, string Email, string Perfil);

public static class IdentidadeEndpoints
{
    public const string PoliticaRateLimitLogin = "login";

    public static IEndpointRouteBuilder MapIdentidadeEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Autenticação");

        auth.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(PoliticaRateLimitLogin);

        auth.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            id = user.FindFirstValue("sub"),
            nome = user.FindFirstValue(TokenService.ClaimNome),
            email = user.FindFirstValue("email"),
            perfil = user.FindFirstValue(TokenService.ClaimPapel)
        }));

        app.MapPost("/api/v1/usuarios", CriarUsuarioAsync)
            .WithTags("Usuários")
            .RequireAuthorization(Politicas.Administracao);

        return app;
    }

    private static async Task<IResult> LoginAsync(LoginRequest req, AutenticacaoService servico, CancellationToken ct)
    {
        var resultado = await servico.LoginAsync(req.Email, req.Senha, ct);

        // Mensagem única para e-mail inexistente ou senha errada: não revela qual dos dois falhou.
        return resultado is null
            ? Results.Problem(title: "Credenciais inválidas.", statusCode: StatusCodes.Status401Unauthorized)
            : Results.Ok(new LoginResponse(resultado.AccessToken, "Bearer", resultado.ExpiraEm, resultado.Perfil.ToString()));
    }

    private static async Task<IResult> CriarUsuarioAsync(
        CriarUsuarioRequest req, CadastroUsuarioService servico, CancellationToken ct)
    {
        var usuario = await servico.CriarAsync(req.Nome ?? "", req.Email ?? "", req.Senha ?? "", req.Perfil, ct);

        return Results.Created(
            $"/api/v1/usuarios/{usuario.Id}",
            new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Email, usuario.Perfil.ToString()));
    }
}
