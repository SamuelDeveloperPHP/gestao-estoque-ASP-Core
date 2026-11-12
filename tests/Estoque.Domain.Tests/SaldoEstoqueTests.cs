using FluentAssertions;
using GestaoEstoque.BuildingBlocks;
using GestaoEstoque.Modules.Estoque.Domain;
using Xunit;

namespace Estoque.Domain.Tests;

public class SaldoEstoqueTests
{
    private static SaldoEstoque Novo() => new(Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Entrada_aumenta_o_saldo_e_gera_evento()
    {
        var saldo = Novo();

        saldo.Registrar(TipoMovimentacao.Entrada, 10);

        saldo.Quantidade.Should().Be(10);
        saldo.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MovimentacaoRegistrada>()
            .Which.SaldoApos.Should().Be(10);
    }

    [Fact]
    public void Saida_reduz_o_saldo()
    {
        var saldo = Novo();
        saldo.Registrar(TipoMovimentacao.Entrada, 10);

        saldo.Registrar(TipoMovimentacao.Saida, 4);

        saldo.Quantidade.Should().Be(6);
    }

    [Fact]
    public void Saida_maior_que_o_saldo_e_rejeitada_sem_alterar_o_estado()
    {
        var saldo = Novo();
        saldo.Registrar(TipoMovimentacao.Entrada, 5);

        var acao = () => saldo.Registrar(TipoMovimentacao.Saida, 6);

        acao.Should().Throw<DomainException>().WithMessage("Saldo insuficiente*");
        saldo.Quantidade.Should().Be(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantidade_nao_positiva_e_rejeitada(decimal quantidade)
    {
        var acao = () => Novo().Registrar(TipoMovimentacao.Entrada, quantidade);

        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void Produto_ou_deposito_vazio_e_rejeitado()
    {
        var acao = () => new SaldoEstoque(Guid.Empty, Guid.NewGuid());

        acao.Should().Throw<DomainException>();
    }
}
