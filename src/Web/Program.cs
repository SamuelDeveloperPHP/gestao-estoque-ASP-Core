using System.Globalization;
using System.Threading.RateLimiting;
using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ApiOptions>()
    .Bind(builder.Configuration.GetSection(ApiOptions.Secao))
    .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _), "Api:BaseUrl deve ser uma URL absoluta.")
    .ValidateOnStart();

// Chaves que protegem o cookie de login. Sem persistir, todo reinício/réplica nova invalida as sessões.
// Em produção/K8s monte um volume compartilhado (PVC) ou use um repositório externo (ex.: Redis/Blob + KMS).
var chaves = builder.Configuration["DataProtection:Path"];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("gestao-estoque-web");
if (!string.IsNullOrWhiteSpace(chaves))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(chaves));

builder.Services.AddObservabilidade(builder.Configuration, "gestao-estoque-web");
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<ApiClient>((sp, client) =>
    {
        client.BaseAddress = new Uri(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiOptions>>().Value.BaseUrl);
    })
    .AddHttpMessageHandler<BearerTokenHandler>()
    // Retry/circuit breaker/timeout. Sem retry em POST: reenvio seguro é responsabilidade da Idempotency-Key.
    .AddStandardResilienceHandler(o =>
        // Só repete GET com resposta transitória (5xx/408/429). POST nunca é repetido automaticamente.
        o.Retry.ShouldHandle = args => ValueTask.FromResult(
            args.Outcome.Result?.RequestMessage?.Method == HttpMethod.Get
            && HttpClientResiliencePredicates.IsTransient(args.Outcome)));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = ".GestaoEstoque.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = Enum.Parse<CookieSecurePolicy>(
            builder.Configuration["Cookie:SecurePolicy"] ?? nameof(CookieSecurePolicy.SameAsRequest), ignoreCase: true);
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AcessoNegado";
        o.SlidingExpiration = false; // a validade é a do JWT emitido pela API
    });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("Gestao", p => p.RequireRole("Gestor", "Administrador"))
    .AddPolicy("Administracao", p => p.RequireRole("Administrador"));

var loginPorMinuto = builder.Configuration.GetValue("RateLimit:LoginPorMinuto", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPorMinuto, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    o.Filters.Add<SessaoExpiradaFilter>();
});
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Erro");
}

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

var pt = new CultureInfo("pt-BR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(pt),
    SupportedCultures = [pt],
    SupportedUICultures = [pt]
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program;
