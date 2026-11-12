using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestaoEstoque.Modules.Catalogo;

public static class AssemblyMarker;

public static class CatalogoModule
{
    // Persistência (schema "catalogo") entra na próxima etapa.
    public static IServiceCollection AddCatalogoModule(this IServiceCollection services, IConfiguration config)
        => services;
}
