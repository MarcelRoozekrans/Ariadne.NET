using Ariadne.NET.Abstractions;

namespace Ariadne.NET.RuleLoader;

/// <summary>Builds the error both the load-time member rules and the resolver's reference rules report.</summary>
internal static class RulePackError
{
    /// <summary>
    /// Builds the error for a rule pack that broke rules: one <c>path: reason</c> line per failure. A named pack is
    /// quoted by name; a merged name keeps only its non-empty parts. A nameless pack falls back to
    /// <paramref name="unnamedSubject"/>, such as the file it came from.
    /// </summary>
    internal static AriadneError Invalid(string? packName, IEnumerable<string> failures, string unnamedSubject = "Rule pack")
    {
        var label = string.Join('+', (packName ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries));
        var message = label.Length == 0 ? $"{unnamedSubject} is invalid." : $"Rule pack '{label}' is invalid.";
        return new AriadneError(AriadneErrorCode.Invalid, message, string.Join('\n', failures));
    }
}
