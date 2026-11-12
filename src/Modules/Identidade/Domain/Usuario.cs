using GestaoEstoque.BuildingBlocks;

namespace GestaoEstoque.Modules.Identidade.Domain;

public enum Perfil
{
    Operador = 1,
    Gestor = 2,
    Administrador = 3
}

public sealed class Usuario : Entity
{
    private Usuario() { } // EF Core

    public Usuario(string nome, string email, Perfil perfil)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new DomainException("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("E-mail inválido.");
        Nome = nome.Trim();
        Email = email.Trim().ToLowerInvariant();
        Perfil = perfil;
    }

    public string Nome { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public Perfil Perfil { get; private set; }
}
