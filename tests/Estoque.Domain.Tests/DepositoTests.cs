using FluentAssertions;
using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Estoque.Domain;
using Xunit;

namespace Estoque.Domain.Tests;

public class DepositoTests
{
    [Fact]
    public void Normaliza_codigo_e_nome_e_nasce_ativo()
    {
        var d = new Deposito("  cd-sp ", "  Centro de Distribuição SP ");

        d.Codigo.Should().Be("CD-SP");
        d.Nome.Should().Be("Centro de Distribuição SP");
        d.Ativo.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Nome")]
    [InlineData("COD", " ")]
    [InlineData("COM ESPACO", "Nome")]
    [InlineData("COD@", "Nome")]
    [InlineData("UM-CODIGO-MUITO-LONGO-DEMAIS", "Nome")]
    public void Dados_invalidos_sao_rejeitados(string codigo, string nome)
    {
        var acao = () => new Deposito(codigo, nome);

        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void Desativar_marca_como_inativo()
    {
        var d = new Deposito("PRINC", "Principal");

        d.Desativar();

        d.Ativo.Should().BeFalse();
    }
}
