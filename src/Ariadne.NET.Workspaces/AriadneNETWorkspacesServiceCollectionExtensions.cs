using Ariadne.NET.Workspaces;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the services that load and check solutions on disk.</summary>
public static class AriadneNETWorkspacesServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISolutionLoader"/> and <see cref="ISolutionValidator"/> as singletons. The validator needs the engine's
    /// <c>IArchitectureValidator</c>, so callers also call <see cref="AriadneNETServiceCollectionExtensions.AddAriadneNETServices"/>.
    /// </summary>
    public static IServiceCollection AddAriadneNETWorkspaces(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISolutionLoader, SolutionLoader>();
        services.AddSingleton<ISolutionValidator, SolutionValidator>();
        return services;
    }
}
