using GestaoEstoque.BuildingBlocks;

namespace GestaoEstoque.Modules.Catalogo.Domain;

public sealed class Produto : Entity
{
    private Produto() { } // EF Core

    public Produto(string sku, string nome, string? unidade)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new DomainException("SKU é obrigatório.");
        if (string.IsNullOrWhiteSpace(nome)) throw new DomainException("Nome é obrigatório.");
        if (sku.Trim().Length > 50) throw new DomainException("SKU deve ter no máximo 50 caracteres.");
        if (nome.Trim().Length > 200) throw new DomainException("Nome deve ter no máximo 200 caracteres.");

        Sku = sku.Trim().ToUpperInvariant();
        Nome = nome.Trim();
        Unidade = string.IsNullOrWhiteSpace(unidade) ? "UN" : unidade.Trim().ToUpperInvariant();

        if (Unidade.Length > 10) throw new DomainException("Unidade deve ter no máximo 10 caracteres.");
    }

    public string Sku { get; private set; } = default!;
    public string Nome { get; private set; } = default!;
    public string Unidade { get; private set; } = "UN";
    public bool Ativo { get; private set; } = true;

    public void Desativar() => Ativo = false;
}
