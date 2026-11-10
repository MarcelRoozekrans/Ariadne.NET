using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;

namespace Ariadne.NET.Cli;

/// <summary>Prints the CLI's informational version without the build metadata after a plus sign, then exits 0.</summary>
internal sealed class VersionAction : SynchronousCommandLineAction
{
    public override int Invoke(ParseResult parseResult)
    {
        var assembly = typeof(VersionAction).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        parseResult.InvocationConfiguration.Output.WriteLine(plus >= 0 ? version[..plus] : version);
        return ExitCodes.Success;
    }
}
