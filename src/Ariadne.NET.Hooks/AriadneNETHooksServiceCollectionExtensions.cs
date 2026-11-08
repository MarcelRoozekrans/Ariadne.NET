using Ariadne.NET.Hooks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the agent hooks.</summary>
public static class AriadneNETHooksServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="HookHost"/> and the hooks it runs as singletons. The hooks need the services of
    /// <c>AddAriadneNETServices()</c> (<c>IRulePackLoader</c>, <c>IRulePackResolver</c> and <c>IImportsLoader</c>) and of
    /// <c>AddAriadneNETWorkspaces()</c> (<c>ISolutionValidator</c>), so call both as well.
    /// </summary>
    public static IServiceCollection AddAriadneNETHooks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IProjectRules, ProjectRules>();
        services.AddSingleton<IHook, SessionStartHook>();
        services.AddSingleton<IHook, PreToolUseHook>();
        services.AddSingleton<IHook, StopHook>();
        services.AddSingleton<HookHost>();
        return services;
    }
}
