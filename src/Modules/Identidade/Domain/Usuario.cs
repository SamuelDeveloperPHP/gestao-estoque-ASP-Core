using System.Text.Json.Serialization;
using GestaoEstoque.BuildingBlocks;

namespace GestaoEstoque.Modules.Identidade.Domain;

/// <summary>Os nomes devem coincidir com <see cref="Papeis"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Perfil
{
    Operador = 1,
    Gestor = 2,
    Administrador = 3
}

public sealed class Usuario : Entity
{
    private Usuario() { } // EF Core

    public Usuario(string nome, string email, Perfil perfil, string senhaHash)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new DomainException("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.Length > 254)
            throw new DomainException("E-mail inválido.");
        if (!Enum.IsDefined(perfil)) throw new DomainException("Perfil inválido.");
        if (string.IsNullOrWhiteSpace(senhaHash)) throw new DomainException("Hash de senha é obrigatório.");

        Nome = nome.Trim();
        Email = email.Trim().ToLowerInvariant();
        Perfil = perfil;
        SenhaHash = senhaHash;
        Ativo = true;
    }

    public string Nome { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public Perfil Perfil { get; private set; }
    public string SenhaHash { get; private set; } = default!;
    public bool Ativo { get; private set; }

    public void Desativar() => Ativo = false;
}
