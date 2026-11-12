using FluentAssertions;
using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Catalogo.Domain;
using Xunit;

namespace Modulos.Domain.Tests;

public class ProdutoTests
{
    [Fact]
    public void Normaliza_sku_nome_e_unidade_padrao()
    {
        var p = new Produto("  ab-123 ", "  Parafuso  ", null);

        p.Sku.Should().Be("AB-123");
        p.Nome.Should().Be("Parafuso");
        p.Unidade.Should().Be("UN");
        p.Ativo.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Nome")]
    [InlineData("SKU", " ")]
    public void Sku_e_nome_sao_obrigatorios(string sku, string nome)
    {
        var acao = () => new Produto(sku, nome, "UN");

        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void Limites_de_tamanho_sao_respeitados()
    {
        var acao = () => new Produto(new string('A', 51), "Nome", "UN");

        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void Desativar_marca_o_produto_como_inativo()
    {
        var p = new Produto("SKU", "Nome", "UN");

        p.Desativar();

        p.Ativo.Should().BeFalse();
    }
}
