using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Estoque.Arch.Tests;

/// <summary>Garante que os módulos não dependam uns dos outros (só do BuildingBlocks).</summary>
public class ModuleIsolationTests
{
    private const string Raiz = "GestaoEstoque.Modules.";

    public static IEnumerable<object[]> Modulos() =>
    [
        [typeof(GestaoEstoque.Modules.Identidade.AssemblyMarker).Assembly, "Identidade"],
        [typeof(GestaoEstoque.Modules.Catalogo.AssemblyMarker).Assembly, "Catalogo"],
        [typeof(GestaoEstoque.Modules.Estoque.AssemblyMarker).Assembly, "Estoque"],
    ];

    [Theory]
    [MemberData(nameof(Modulos))]
    public void Modulo_nao_depende_de_outros_modulos(Assembly assembly, string nome)
    {
        var outros = new[] { "Identidade", "Catalogo", "Estoque" }
            .Where(m => m != nome)
            .Select(m => Raiz + m)
            .ToArray();

        var resultado = Types.InAssembly(assembly)
            .ShouldNot().HaveDependencyOnAny(outros)
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue(
            "o módulo {0} só pode depender do BuildingBlocks. Violações: {1}",
            nome, string.Join(", ", resultado.FailingTypeNames ?? []));
    }
}
