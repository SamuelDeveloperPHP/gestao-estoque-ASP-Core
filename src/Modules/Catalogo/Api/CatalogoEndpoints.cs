using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Catalogo.Application;
using GestaoEstoque.Modules.Catalogo.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GestaoEstoque.Modules.Catalogo.Api;

public sealed record CriarProdutoRequest(string? Sku, string? Nome, string? Unidade);

public sealed record ProdutoResponse(Guid Id, string Sku, string Nome, string Unidade, bool Ativo)
{
    public static ProdutoResponse De(Produto p) => new(p.Id, p.Sku, p.Nome, p.Unidade, p.Ativo);
}

public static class CatalogoEndpoints
{
    public static IEndpointRouteBuilder MapCatalogoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/produtos").WithTags("Catálogo");

        // Leitura: qualquer usuário autenticado (política padrão). Escrita: Gestor ou Administrador.
        grupo.MapGet("/", ListarAsync);
        grupo.MapGet("/{id:guid}", ObterAsync);
        grupo.MapPost("/", CriarAsync).RequireAuthorization(Politicas.Gestao);
        grupo.MapPost("/{id:guid}/desativar", DesativarAsync).RequireAuthorization(Politicas.Gestao);

        return app;
    }

    private static async Task<IResult> ListarAsync(
        ProdutoService servico, string? q, int? skip, int? take, CancellationToken ct)
    {
        var itens = await servico.ListarAsync(q, skip ?? 0, take ?? 50, ct);
        return Results.Ok(itens.Select(ProdutoResponse.De));
    }

    private static async Task<IResult> ObterAsync(Guid id, ProdutoService servico, CancellationToken ct)
    {
        var produto = await servico.ObterAsync(id, ct);
        return produto is null ? Results.NotFound() : Results.Ok(ProdutoResponse.De(produto));
    }

    private static async Task<IResult> CriarAsync(CriarProdutoRequest req, ProdutoService servico, CancellationToken ct)
    {
        var produto = await servico.CriarAsync(req.Sku ?? "", req.Nome ?? "", req.Unidade, ct);
        return Results.Created($"/api/v1/produtos/{produto.Id}", ProdutoResponse.De(produto));
    }

    private static async Task<IResult> DesativarAsync(Guid id, ProdutoService servico, CancellationToken ct) =>
        await servico.DesativarAsync(id, ct) ? Results.NoContent() : Results.NotFound();
}
