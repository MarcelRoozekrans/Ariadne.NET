using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Mcp;

/// <summary>The one text form of an <see cref="AriadneError"/> that tool errors and resource errors carry.</summary>
internal static class ErrorText
{
    /// <summary>The message, then the detail on the next line when there is one, as the CLI prints them.</summary>
    internal static string Format(AriadneError error) =>
        error.Detail is null ? error.Message : $"{error.Message}\n{error.Detail}";
}
