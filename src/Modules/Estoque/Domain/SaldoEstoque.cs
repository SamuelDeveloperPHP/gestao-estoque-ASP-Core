using GestaoEstoque.BuildingBlocks;

namespace GestaoEstoque.Modules.Estoque.Domain;

public enum TipoMovimentacao
{
    Entrada = 1,
    Saida = 2
}

public sealed record MovimentacaoRegistrada(
    Guid ProdutoId,
    Guid DepositoId,
    TipoMovimentacao Tipo,
    decimal Quantidade,
    decimal SaldoApos) : IDomainEvent
{
    public DateTime OcorridoEm { get; } = DateTime.UtcNow;
}

/// <summary>
/// Saldo de um produto em um depósito. Regra central: o saldo nunca fica negativo.
/// </summary>
public sealed class SaldoEstoque : Entity
{
    private SaldoEstoque() { } // EF Core

    public SaldoEstoque(Guid produtoId, Guid depositoId)
    {
        if (produtoId == Guid.Empty) throw new DomainException("Produto inválido.");
        if (depositoId == Guid.Empty) throw new DomainException("Depósito inválido.");
        ProdutoId = produtoId;
        DepositoId = depositoId;
    }

    public Guid ProdutoId { get; private set; }
    public Guid DepositoId { get; private set; }
    public decimal Quantidade { get; private set; }

    public void Registrar(TipoMovimentacao tipo, decimal quantidade)
    {
        if (quantidade <= 0) throw new DomainException("A quantidade deve ser maior que zero.");

        var novoSaldo = tipo switch
        {
            TipoMovimentacao.Entrada => Quantidade + quantidade,
            TipoMovimentacao.Saida => Quantidade - quantidade,
            _ => throw new DomainException("Tipo de movimentação inválido.")
        };

        if (novoSaldo < 0) throw new DomainException("Saldo insuficiente para a saída.");

        Quantidade = novoSaldo;
        Raise(new MovimentacaoRegistrada(ProdutoId, DepositoId, tipo, quantidade, novoSaldo));
    }
}
