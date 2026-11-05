using Ariadne.NET.Abstractions;
using Microsoft.Build.Locator;

namespace Ariadne.NET.Workspaces;

/// <summary>
/// Points the process at the .NET SDK's MSBuild once, so <c>Microsoft.Build.Framework</c> loads from the SDK. Kept apart from the
/// code that opens a workspace, so no MSBuild type is loaded before the registration.
/// </summary>
internal static class MSBuildLocatorRegistration
{
    private static readonly Lock Gate = new();

    /// <summary>
    /// Registers the default .NET SDK unless one is registered. Returns the error when no SDK is found, or when MSBuild assemblies
    /// are already loaded in this process, which makes registration impossible.
    /// </summary>
    public static AriadneError? EnsureRegistered()
    {
        lock (Gate)
        {
            if (MSBuildLocator.IsRegistered)
            {
                return null;
            }

            if (!MSBuildLocator.QueryVisualStudioInstances().Any())
            {
                return new AriadneError(AriadneErrorCode.Unavailable, "No .NET SDK was found to load the solution with.");
            }

            if (!MSBuildLocator.CanRegister)
            {
                return new AriadneError(
                    AriadneErrorCode.Unavailable,
                    "MSBuild assemblies were loaded before the .NET SDK could be registered.",
                    "MSBuildLocator must register before any Microsoft.Build assembly is loaded; this is a fault in the host process.");
            }

            MSBuildLocator.RegisterDefaults();
            return null;
        }
    }
}
