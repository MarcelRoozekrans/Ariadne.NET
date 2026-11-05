using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Cli;

/// <summary>
/// Reports an expected failure the way every command does: the message, then the detail when there is one.
/// </summary>
internal static class ErrorOutput
{
    internal static int Report(AriadneError error)
    {
        Console.Error.WriteLine($"Error: {error.Message}");
        if (error.Detail is not null)
        {
            Console.Error.WriteLine(error.Detail);
        }

        return ExitCodes.Error;
    }
}
