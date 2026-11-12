using System.Threading.RateLimiting;
using GestaoEstoque.Api.Integracao;
using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Catalogo;
using GestaoEstoque.Modules.Catalogo.Api;
using GestaoEstoque.Modules.Estoque;
using GestaoEstoque.Modules.Estoque.Api;
using GestaoEstoque.Modules.Estoque.Application;
using GestaoEstoque.Modules.Identidade;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using GestaoEstoque.Modules.Identidade.Api;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddIdentidadeModule(builder.Configuration)
    .AddCatalogoModule(builder.Configuration)
    .AddEstoqueModule(builder.Configuration);

// Composition root: liga a porta do Estoque ao contrato público do Catálogo.
builder.Services.AddScoped<IVerificadorProduto, ProdutoCatalogoAdapter>();

// Opt-in: só habilite quando um chamador confiável (ex.: o Web) envia o IP real em X-Forwarded-For
// e a API NÃO está exposta diretamente; senão o cabeçalho pode ser forjado para burlar o rate limit.
var confiarEmForwardedFor = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
if (confiarEmForwardedFor)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

builder.Services.AddObservabilidade(builder.Configuration, "gestao-estoque-api");

// Consumidor de exemplo dos eventos (só quando o RabbitMQ está configurado).
if (!string.IsNullOrWhiteSpace(builder.Configuration["RabbitMq:Host"]))
    builder.Services.AddHostedService<EventosEstoqueConsumer>();

builder.Services.AddProblemDetails();
builder.Services.AddResponseCompression();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!, name: "postgres", tags: ["ready"]);

// Proteção contra força bruta no login (por IP).
var loginPorMinuto = builder.Configuration.GetValue("RateLimit:LoginPorMinuto", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(IdentidadeEndpoints.PoliticaRateLimitLogin, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPorMinuto,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Gestão de Estoque API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Faça login em /api/v1/auth/login e cole aqui apenas o accessToken."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

var app = builder.Build();

if (confiarEmForwardedFor) app.UseForwardedHeaders();

// Qualquer exceção inesperada: 500 sem vazar detalhes (e com log de erro).
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

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseResponseCompression();

// O TLS é terminado no ingress/gateway; por isso não há UseHttpsRedirection aqui.
if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.DocumentTitle = "Gestão de Estoque API");
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// liveness: o processo está de pé. readiness: dependências (PostgreSQL) respondem.
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") }).AllowAnonymous();

app.MapGet("/", () => Results.Ok(new { servico = "gestao-estoque", versao = "v1" })).AllowAnonymous();

app.MapIdentidadeEndpoints();
app.MapCatalogoEndpoints();
app.MapEstoqueEndpoints();

// Local: Database__MigrateOnStartup=true. Produção (várias réplicas): rode um Job com "--migrate-only",
// que aplica as migrations, cria o administrador inicial e encerra sem subir o servidor.
var somenteMigrar = args.Contains("--migrate-only");
if (somenteMigrar || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrarIdentidadeAsync();
    await app.Services.MigrarCatalogoAsync();
    await app.Services.MigrarEstoqueAsync();
}

await app.Services.SemearAdministradorAsync(app.Configuration);

if (somenteMigrar) return;

app.Run();

public partial class Program; // permite WebApplicationFactory nos testes de integração
