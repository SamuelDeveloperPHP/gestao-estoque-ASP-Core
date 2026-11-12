using System.Security.Claims;
using GestaoEstoque.Modules.Estoque.Application;
using GestaoEstoque.Modules.Estoque.Domain;
using GestaoEstoque.Modules.Estoque.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GestaoEstoque.Modules.Estoque.Api;

public sealed record MovimentacaoRequest(Guid ProdutoId, Guid DepositoId, TipoMovimentacao Tipo, decimal Quantidade);

public sealed record SaldoResponse(Guid Id, Guid ProdutoId, Guid DepositoId, decimal Quantidade);

public static class EstoqueEndpoints
{
    public static IEndpointRouteBuilder MapEstoqueEndpoints(this IEndpointRouteBuilder app)
    {
        // Qualquer usuário autenticado (operadores incluídos) pode movimentar e consultar.
        var grupo = app.MapGroup("/api/v1/estoque").WithTags("Estoque").RequireAuthorization();

        grupo.MapPost("/movimentacoes", RegistrarAsync);
        grupo.MapGet("/saldos", ListarSaldosAsync);

        app.MapDepositoEndpoints();

        return app;
    }

    private static async Task<IResult> RegistrarAsync(
        [FromHeader(Name = "Idempotency-Key")] string? chave,
        MovimentacaoRequest req,
        RegistrarMovimentacaoHandler handler,
        ClaimsPrincipal usuario,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(chave))
            return Results.Problem(
                title: "O cabeçalho Idempotency-Key é obrigatório.",
                statusCode: StatusCodes.Status400BadRequest);

        if (!Enum.IsDefined(req.Tipo))
            return Results.Problem(title: "Tipo de movimentação inválido.", statusCode: StatusCodes.Status400BadRequest);

        var resultado = await handler.HandleAsync(
            new RegistrarMovimentacaoCommand(req.ProdutoId, req.DepositoId, req.Tipo, req.Quantidade, chave,
                Guid.TryParse(usuario.FindFirstValue("sub"), out var autor) ? autor : null), ct);

        var corpo = new { movimentacaoId = resultado.MovimentacaoId, saldoApos = resultado.SaldoApos };
        return resultado.Repetida ? Results.Ok(corpo) : Results.Created($"/api/v1/estoque/movimentacoes/{resultado.MovimentacaoId}", corpo);
    }

    // Paginação simples (skip/take, máx. 200). Cursor/keyset entra quando o volume exigir.
    private static async Task<IResult> ListarSaldosAsync(
        EstoqueDbContext db,
        Guid? produtoId,
        Guid? depositoId,
        int? skip,
        int? take,
        CancellationToken ct)
    {
        var limite = Math.Clamp(take ?? 50, 1, 200);

        var consulta = db.Saldos.AsNoTracking().AsQueryable();
        if (produtoId is not null) consulta = consulta.Where(s => s.ProdutoId == produtoId);
        if (depositoId is not null) consulta = consulta.Where(s => s.DepositoId == depositoId);

        var itens = await consulta
            .OrderBy(s => s.ProdutoId).ThenBy(s => s.DepositoId)
            .Skip(Math.Max(skip ?? 0, 0))
            .Take(limite)
            .Select(s => new SaldoResponse(s.Id, s.ProdutoId, s.DepositoId, s.Quantidade))
            .ToListAsync(ct);

        return Results.Ok(itens);
    }
}
