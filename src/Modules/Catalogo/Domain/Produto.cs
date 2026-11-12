using GestaoEstoque.BuildingBlocks;

namespace GestaoEstoque.Modules.Catalogo.Domain;

public sealed class Produto : Entity
{
    private Produto() { } // EF Core

    public Produto(string sku, string nome, string unidade)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new DomainException("SKU é obrigatório.");
        if (string.IsNullOrWhiteSpace(nome)) throw new DomainException("Nome é obrigatório.");
        Sku = sku.Trim().ToUpperInvariant();
        Nome = nome.Trim();
        Unidade = string.IsNullOrWhiteSpace(unidade) ? "UN" : unidade.Trim().ToUpperInvariant();
    }

    public string Sku { get; private set; } = default!;
    public string Nome { get; private set; } = default!;
    public string Unidade { get; private set; } = "UN";
    public bool Ativo { get; private set; } = true;

    public void Desativar() => Ativo = false;
}
