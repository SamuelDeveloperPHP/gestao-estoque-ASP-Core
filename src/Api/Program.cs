using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Catalogo;
using GestaoEstoque.Modules.Estoque;
using GestaoEstoque.Modules.Identidade;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddIdentidadeModule(builder.Configuration)
    .AddCatalogoModule(builder.Configuration)
    .AddEstoqueModule(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!, name: "postgres", tags: ["ready"]);

var app = builder.Build();

// Regras de domínio violadas viram 422; qualquer outra exceção, 500 sem vazar detalhes.
app.UseExceptionHandler(handler => handler.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, titulo) = ex is DomainException
        ? (StatusCodes.Status422UnprocessableEntity, ex.Message)
        : (StatusCodes.Status500InternalServerError, "Erro interno.");
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = titulo });
}));

app.UseHttpsRedirection();

// liveness: o processo está de pé. readiness: dependências (PostgreSQL) respondem.
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });

app.MapGet("/api/v1", () => Results.Ok(new { servico = "gestao-estoque", versao = "v1" }));

app.Run();

public partial class Program; // permite WebApplicationFactory nos testes de integração
