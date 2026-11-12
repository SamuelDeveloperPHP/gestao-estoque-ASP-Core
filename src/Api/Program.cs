using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Catalogo;
using GestaoEstoque.Modules.Estoque;
using GestaoEstoque.Modules.Estoque.Api;
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

// Qualquer outra exceção: 500 sem vazar detalhes (e com log de erro).
app.UseExceptionHandler(handler => handler.Run(async ctx =>
{
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await ctx.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = "Erro interno."
    });
}));

// Regra de domínio violada é erro do cliente: vira 422 sem poluir o log com stack trace.
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (DomainException ex)
    {
        ctx.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = ex.Message
        });
    }
});

app.UseHttpsRedirection();

// liveness: o processo está de pé. readiness: dependências (PostgreSQL) respondem.
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });

app.MapGet("/api/v1", () => Results.Ok(new { servico = "gestao-estoque", versao = "v1" }));
app.MapEstoqueEndpoints();

// Em produção com várias réplicas, prefira rodar as migrations em um Job do K8s.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrarEstoqueAsync();
}

app.Run();

public partial class Program; // permite WebApplicationFactory nos testes de integração
