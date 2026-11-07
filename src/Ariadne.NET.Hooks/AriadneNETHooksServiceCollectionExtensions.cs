using Ariadne.NET.Hooks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the agent hooks.</summary>
public static class AriadneNETHooksServiceCollectionExtensions
{
    /// <summary>Registers <see cref="HookHost"/> and the hooks it runs as singletons.</summary>
    public static IServiceCollection AddAriadneNETHooks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IProjectRules, ProjectRules>();
        services.AddSingleton<IHook, SessionStartHook>();
        services.AddSingleton<IHook, PreToolUseHook>();
        services.AddSingleton<HookHost>();
        return services;
    }
}
