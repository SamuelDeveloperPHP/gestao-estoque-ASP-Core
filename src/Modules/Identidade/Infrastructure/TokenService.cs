using System.Security.Claims;
using System.Text;
using GestaoEstoque.Modules.Identidade.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GestaoEstoque.Modules.Identidade.Infrastructure;

public sealed class JwtOptions
{
    public const string Secao = "Jwt";
    public const int TamanhoMinimoChave = 32;

    public string Issuer { get; set; } = "gestao-estoque";
    public string Audience { get; set; } = "gestao-estoque-api";

    /// <summary>Segredo HMAC-SHA256. Nunca versionar: vem de variável de ambiente / secret.</summary>
    public string Key { get; set; } = "";

    public int ExpiraMinutos { get; set; } = 60;
}

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public const string ClaimPapel = "role";
    public const string ClaimNome = "name";

    public (string Token, DateTime ExpiraEm) Gerar(Usuario usuario)
    {
        var o = options.Value;
        var agora = clock.GetUtcNow().UtcDateTime;
        var expira = agora.AddMinutes(o.ExpiraMinutos);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
                new Claim(ClaimNome, usuario.Nome),
                new Claim(ClaimPapel, usuario.Perfil.ToString())
            }),
            Issuer = o.Issuer,
            Audience = o.Audience,
            NotBefore = agora,
            Expires = expira,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expira);
    }
}
