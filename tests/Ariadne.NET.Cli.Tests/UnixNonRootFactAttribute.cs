namespace Ariadne.NET.Cli.Tests;

/// <summary>
/// A fact that needs Unix file modes to take effect: skipped on Windows, and as root, which ignores file permissions.
/// </summary>
public sealed class UnixNonRootFactAttribute : FactAttribute
{
    public UnixNonRootFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Unix file modes are not available on Windows.";
        }
        else if (string.Equals(Environment.UserName, "root", StringComparison.Ordinal))
        {
            Skip = "root ignores file permissions.";
        }
    }
}
