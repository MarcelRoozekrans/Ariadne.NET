namespace Ariadne.NET.Tests;

/// <summary>
/// A fact that creates symbolic links: skipped on Windows, where that needs a privilege or developer mode the test run may lack.
/// </summary>
public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Creating symbolic links on Windows needs a privilege the test run may lack.";
        }
    }
}
