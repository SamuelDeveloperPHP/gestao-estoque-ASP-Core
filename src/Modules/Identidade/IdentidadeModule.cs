using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestaoEstoque.Modules.Identidade;

public static class AssemblyMarker;

public static class IdentidadeModule
{
    // JWT/OIDC e persistência (schema "identidade") entram na próxima etapa.
    public static IServiceCollection AddIdentidadeModule(this IServiceCollection services, IConfiguration config)
        => services;
}
