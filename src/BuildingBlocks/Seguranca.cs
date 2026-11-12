namespace GestaoEstoque.BuildingBlocks;

/// <summary>Valores do claim "role" (iguais aos nomes do enum Perfil).</summary>
public static class Papeis
{
    public const string Operador = "Operador";
    public const string Gestor = "Gestor";
    public const string Administrador = "Administrador";
}

/// <summary>Nomes das políticas de autorização compartilhadas entre módulos.</summary>
public static class Politicas
{
    /// <summary>Gestor ou Administrador.</summary>
    public const string Gestao = "Gestao";

    /// <summary>Somente Administrador.</summary>
    public const string Administracao = "Administracao";
}
