using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Estoque.Application;
using GestaoEstoque.Modules.Estoque.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GestaoEstoque.Modules.Estoque.Api;

public sealed record CriarDepositoRequest(string? Codigo, string? Nome);

public sealed record DepositoResponse(Guid Id, string Codigo, string Nome, bool Ativo)
{
    public static DepositoResponse De(Deposito d) => new(d.Id, d.Codigo, d.Nome, d.Ativo);
}

public static class DepositoEndpoints
{
    public static IEndpointRouteBuilder MapDepositoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/depositos").WithTags("Depósitos");

        // Leitura: qualquer usuário autenticado. Escrita: Gestor ou Administrador.
        grupo.MapGet("/", ListarAsync);
        grupo.MapPost("/", CriarAsync).RequireAuthorization(Politicas.Gestao);
        grupo.MapPost("/{id:guid}/desativar", DesativarAsync).RequireAuthorization(Politicas.Gestao);

        return app;
    }

    private static async Task<IResult> ListarAsync(
        DepositoService servico, HttpResponse resposta, bool? apenasAtivos, CancellationToken ct)
    {
        var itens = await servico.ListarAsync(apenasAtivos ?? false, ct);
        resposta.Headers["X-Total-Count"] = itens.Count.ToString();
        return Results.Ok(itens.Select(DepositoResponse.De));
    }

    private static async Task<IResult> CriarAsync(CriarDepositoRequest req, DepositoService servico, CancellationToken ct)
    {
        var deposito = await servico.CriarAsync(req.Codigo ?? "", req.Nome ?? "", ct);
        return Results.Created($"/api/v1/depositos/{deposito.Id}", DepositoResponse.De(deposito));
    }

    private static async Task<IResult> DesativarAsync(Guid id, DepositoService servico, CancellationToken ct) =>
        await servico.DesativarAsync(id, ct) ? Results.NoContent() : Results.NotFound();
}
