using GestaoEstoque.BuildingBlocks;

namespace GestaoEstoque.Modules.Identidade.Application;

public static class PoliticaSenha
{
    public const int TamanhoMinimo = 12;

    public static void Validar(string? senha)
    {
        if (string.IsNullOrEmpty(senha) || senha.Length < TamanhoMinimo)
            throw new DomainException($"A senha deve ter ao menos {TamanhoMinimo} caracteres.");
        if (senha.Length > 128)
            throw new DomainException("A senha deve ter no máximo 128 caracteres.");
        if (!senha.Any(char.IsLetter) || !senha.Any(char.IsDigit))
            throw new DomainException("A senha deve conter letras e números.");
    }
}
