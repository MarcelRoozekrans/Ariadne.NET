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
        var label = string.Join('+', (packName ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries).Select(PackText.Show));
        var message = label.Length == 0 ? $"{unnamedSubject} is invalid." : $"Rule pack '{label}' is invalid.";
        return new AriadneError(AriadneErrorCode.Invalid, message, string.Join('\n', failures));
    }

    /// <summary>
    /// Builds the error for a pack the reader rejected: <c>&lt;subject&gt; has &lt;n&gt; problem(s).</c>, with one
    /// <c>&lt;location&gt;:&lt;line&gt;:&lt;column&gt;: &lt;message&gt;</c> line per problem in the detail.
    /// </summary>
    /// <param name="subject">Starts the message, such as <c>Rule pack file '…'</c> or <c>Rule pack YAML</c>.</param>
    /// <param name="location">Starts every detail line: the file path, or <c>rule pack YAML</c> for a string.</param>
    /// <param name="problems">The problems, in document order; at least one.</param>
    internal static AriadneError Problems(string subject, string location, IReadOnlyList<PackProblem> problems)
    {
        var lines = new string[problems.Count];
        for (var i = 0; i < problems.Count; i++)
        {
            var problem = problems[i];
            lines[i] = $"{location}:{problem.Line}:{problem.Column}: {problem.Message}";
        }

        return new AriadneError(
            AriadneErrorCode.Invalid,
            $"{subject} has {problems.Count} problem(s).",
            string.Join('\n', lines));
    }
}
