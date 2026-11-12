using FluentAssertions;
using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Identidade.Application;
using GestaoEstoque.Modules.Identidade.Domain;
using GestaoEstoque.Modules.Identidade.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Modulos.Domain.Tests;

public class UsuarioTests
{
    [Fact]
    public void Normaliza_email_e_nome()
    {
        var u = new Usuario("  Ana  ", "  ANA@Empresa.COM ", Perfil.Gestor, "hash");

        u.Email.Should().Be("ana@empresa.com");
        u.Nome.Should().Be("Ana");
        u.Ativo.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "a@b.com", "hash")]
    [InlineData("Ana", "sem-arroba", "hash")]
    [InlineData("Ana", "a@b.com", "")]
    public void Dados_invalidos_sao_rejeitados(string nome, string email, string hash)
    {
        var acao = () => new Usuario(nome, email, Perfil.Operador, hash);

        acao.Should().Throw<DomainException>();
    }
}

public class PoliticaSenhaTests
{
    [Theory]
    [InlineData("curta1")]
    [InlineData("somenteletrasaqui")]
    [InlineData("1234567890123")]
    [InlineData("")]
    public void Senhas_fracas_sao_rejeitadas(string senha)
    {
        var acao = () => PoliticaSenha.Validar(senha);

        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void Senha_forte_e_aceita()
    {
        var acao = () => PoliticaSenha.Validar("Estoque2026ok");

        acao.Should().NotThrow();
    }
}

public class TokenServiceTests
{
    private static TokenService Servico(int minutos = 60) => new(
        Options.Create(new JwtOptions { Key = new string('k', 40), ExpiraMinutos = minutos }),
        TimeProvider.System);

    [Fact]
    public void Token_carrega_identidade_e_papel()
    {
        var usuario = new Usuario("Ana", "ana@empresa.com", Perfil.Gestor, "hash");

        var (token, expira) = Servico().Gerar(usuario);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        jwt.GetClaim(TokenService.ClaimPapel).Value.Should().Be("Gestor");
        jwt.GetClaim("email").Value.Should().Be("ana@empresa.com");
        jwt.Subject.Should().Be(usuario.Id.ToString());
        expira.Should().BeAfter(DateTime.UtcNow.AddMinutes(59));
    }

    [Fact]
    public void Papeis_do_token_coincidem_com_as_constantes_de_autorizacao()
    {
        Enum.GetNames<Perfil>().Should().BeEquivalentTo(
            [Papeis.Operador, Papeis.Gestor, Papeis.Administrador]);
    }
}
