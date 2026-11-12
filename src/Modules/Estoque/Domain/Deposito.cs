using System.Text.RegularExpressions;
using GestaoEstoque.BuildingBlocks;

namespace GestaoEstoque.Modules.Estoque.Domain;

/// <summary>Local físico onde o estoque é mantido. Saldos e movimentações referenciam o depósito pelo Id.</summary>
public sealed partial class Deposito : Entity
{
    /// <summary>Depósito criado pela migration para preservar saldos anteriores ao cadastro de depósitos.</summary>
    public static readonly Guid PrincipalId = new("00000000-0000-0000-0000-000000000001");

    private Deposito() { } // EF Core

    public Deposito(string codigo, string nome)
    {
        if (string.IsNullOrWhiteSpace(codigo)) throw new DomainException("Código é obrigatório.");
        if (string.IsNullOrWhiteSpace(nome)) throw new DomainException("Nome é obrigatório.");

        var cod = codigo.Trim().ToUpperInvariant();
        if (!CodigoValido().IsMatch(cod))
            throw new DomainException("Código deve ter 1 a 20 caracteres: letras, números, '-' ou '_'.");
        if (nome.Trim().Length > 100) throw new DomainException("Nome deve ter no máximo 100 caracteres.");

        Codigo = cod;
        Nome = nome.Trim();
        Ativo = true;
    }

    public string Codigo { get; private set; } = default!;
    public string Nome { get; private set; } = default!;
    public bool Ativo { get; private set; }

    public void Desativar() => Ativo = false;

    [GeneratedRegex("^[A-Z0-9_-]{1,20}$")]
    private static partial Regex CodigoValido();
}
