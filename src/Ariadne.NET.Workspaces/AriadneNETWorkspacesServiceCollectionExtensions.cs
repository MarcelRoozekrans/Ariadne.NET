using Ariadne.NET.Workspaces;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the services that load and check solutions on disk.</summary>
public static class AriadneNETWorkspacesServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISolutionLoader"/>, <see cref="ISolutionValidator"/> and <see cref="IPackSetValidator"/> as singletons. The validators need the engine's
    /// <c>IArchitectureValidator</c> and the pack loader and resolver, so callers also call <see cref="AriadneNETServiceCollectionExtensions.AddAriadneNETServices"/>.
    /// </summary>
    public static IServiceCollection AddAriadneNETWorkspaces(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISolutionLoader, SolutionLoader>();
        services.AddSingleton<ISolutionValidator, SolutionValidator>();
        services.AddSingleton<IPackSetValidator, PackSetValidator>();
        return services;
    }
}
