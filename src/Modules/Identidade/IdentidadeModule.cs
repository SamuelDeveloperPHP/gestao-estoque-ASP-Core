using System.Text;
using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Identidade.Application;
using GestaoEstoque.Modules.Identidade.Domain;
using GestaoEstoque.Modules.Identidade.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GestaoEstoque.Modules.Identidade;

public static class AssemblyMarker;

public static class IdentidadeModule
{
    public static IServiceCollection AddIdentidadeModule(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Postgres")
                 ?? throw new InvalidOperationException("ConnectionStrings:Postgres não configurada.");

        services.AddDbContext<IdentidadeDbContext>(o =>
            o.UseNpgsql(cs, n => n.MigrationsHistoryTable("__ef_migrations", IdentidadeDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.AddScoped<TokenService>();
        services.AddScoped<AutenticacaoService>();
        services.AddScoped<CadastroUsuarioService>();

        // Falha na inicialização se o segredo estiver ausente ou fraco.
        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.Secao))
            .Validate(o => o.Key.Length >= JwtOptions.TamanhoMinimoChave,
                $"Jwt:Key deve ter ao menos {JwtOptions.TamanhoMinimoChave} caracteres.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configuração tardia: as opções só são lidas quando o primeiro token é validado.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var o = jwt.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = o.Issuer,
                    ValidateAudience = true,
                    ValidAudience = o.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RoleClaimType = TokenService.ClaimPapel,
                    NameClaimType = TokenService.ClaimNome
                };
            });

        // Seguro por padrão: todo endpoint exige usuário autenticado, salvo [AllowAnonymous].
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Politicas.Gestao, p => p.RequireRole(Papeis.Gestor, Papeis.Administrador))
            .AddPolicy(Politicas.Administracao, p => p.RequireRole(Papeis.Administrador));

        return services;
    }

    public static async Task MigrarIdentidadeAsync(this IServiceProvider provider, CancellationToken ct = default)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentidadeDbContext>().Database.MigrateAsync(ct);
    }

    /// <summary>
    /// Cria o primeiro administrador quando não há nenhum usuário e as credenciais vêm da configuração
    /// (Identidade:AdminEmail / Identidade:AdminSenha). Sem isso, nada é criado.
    /// </summary>
    public static async Task SemearAdministradorAsync(
        this IServiceProvider provider, IConfiguration config, CancellationToken ct = default)
    {
        var email = config["Identidade:AdminEmail"];
        var senha = config["Identidade:AdminSenha"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha)) return;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentidadeDbContext>();
        if (await db.Usuarios.AnyAsync(ct)) return;

        try
        {
            await scope.ServiceProvider.GetRequiredService<CadastroUsuarioService>()
                .CriarAsync("Administrador", email, senha, Perfil.Administrador, ct);
        }
        catch (DomainException e) when (e.Message.Contains("já cadastrado", StringComparison.Ordinal))
        {
            // Outra réplica criou o administrador ao mesmo tempo: nada a fazer.
        }
    }
}
