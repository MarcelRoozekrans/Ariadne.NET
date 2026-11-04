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

    /// <summary>Registers the default .NET SDK unless one is registered; returns the error when no SDK is found.</summary>
    public static AriadneError? EnsureRegistered()
    {
        lock (Gate)
        {
            if (MSBuildLocator.IsRegistered)
            {
                return null;
            }

            try
            {
                MSBuildLocator.RegisterDefaults();
                return null;
            }
            catch (InvalidOperationException ex)
            {
                return new AriadneError(
                    AriadneErrorCode.Unavailable,
                    "No .NET SDK was found to load the solution with.",
                    ex.Message);
            }
        }
    }
}
