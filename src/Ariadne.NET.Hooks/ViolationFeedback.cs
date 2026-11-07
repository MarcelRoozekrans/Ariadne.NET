using System.Globalization;
using System.Text;
using Ariadne.NET.Analysis;

namespace Ariadne.NET.Hooks;

/// <summary>Turns a validation report into the text that goes back to the agent.</summary>
public static class ViolationFeedback
{
    /// <summary>The most characters the feedback may have.</summary>
    internal const int MaxLength = 10_000;

    private const int MaxField = 400;

    /// <summary>
    /// Builds the feedback for <paramref name="report"/>. It lists at most <paramref name="maxViolations"/> violations, fewer
    /// when the text would pass <see cref="MaxLength"/>, and ends with the number that were left out.
    /// </summary>
    public static string Build(ArchitectureReport report, string root, int maxViolations = 20)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(root);

        var total = report.Violations.Count;
        var text = new StringBuilder(string.Create(CultureInfo.InvariantCulture, $"Ariadne found {total} architecture violation(s) in the solution after your edits this turn:"));

        // The tail names the number left out, so it is at most this long for any count.
        const int tailReserve = 100;
        var listed = 0;
        foreach (var violation in report.Violations.Take(Math.Max(maxViolations, 0)))
        {
            var entry = Entry(violation, root);
            if (text.Length + entry.Length + tailReserve > MaxLength)
            {
                break;
            }

            text.Append(entry);
            listed++;
        }

        if (listed < total)
        {
            text.Append(string.Create(CultureInfo.InvariantCulture, $"\nand {total - listed} more. Run 'ariadne validate' for the full list."));
        }

        return text.ToString();
    }

    private static string Entry(ArchitectureViolation violation, string root)
    {
        var where = violation.Location is { } location
            ? $"{Path.GetRelativePath(root, location.FilePath).Replace('\\', '/')}:{location.Line}"
            : violation.SourceType;
        var entry = new StringBuilder()
            .Append("\n[").Append(violation.Kind).Append("] ").Append(TextLimit.Shorten(where, MaxField))
            .Append("\n  ").Append(TextLimit.Shorten(violation.Message, MaxField));
        if (violation.Because.Length > 0)
        {
            entry.Append("\n  Rule ").Append(TextLimit.Shorten(violation.RuleId, MaxField)).Append(": ").Append(TextLimit.Shorten(violation.Because, MaxField));
        }

        return entry.ToString();
    }
}
