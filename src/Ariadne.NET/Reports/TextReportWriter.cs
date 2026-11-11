using Ariadne.NET.Analysis;

namespace Ariadne.NET.Reports;

/// <summary>Writes an <see cref="ArchitectureReport"/> as the plain text the CLI prints.</summary>
public static class TextReportWriter
{
    /// <summary>
    /// Writes the summary line, any skipped projects and the verdict, then one entry per violation: its kind, location and
    /// pack, its message, and the rule id with the rule's <c>because</c> when it has one.
    /// </summary>
    /// <param name="output">Where the text goes.</param>
    /// <param name="report">The report to write.</param>
    /// <param name="root">The directory violation file paths are made relative to.</param>
    public static void Write(TextWriter output, ArchitectureReport report, string root)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(root);

        output.WriteLine($"Checked {report.TypesChecked} types in {report.Projects.Count} projects");
        foreach (var skipped in report.SkippedProjects)
        {
            output.WriteLine($"Skipped {skipped.Name}: {skipped.Reason}");
        }

        if (report.IsValid)
        {
            output.WriteLine("Result: PASS - No violations found.");
            return;
        }

        output.WriteLine($"Result: FAIL - {report.Violations.Count} violation(s) found.");
        output.WriteLine();
        foreach (var violation in report.Violations)
        {
            var where = violation.Location is { } location
                ? $"{Path.GetRelativePath(root, location.FilePath).Replace('\\', '/')}:{location.Line}"
                : violation.SourceType;
            var frameworks = violation.TargetFrameworks.Count > 0
                ? $" ({string.Join(", ", violation.TargetFrameworks)})"
                : string.Empty;
            var pack = violation.Pack.Length > 0 ? $" pack {violation.Pack}" : string.Empty;
            output.WriteLine($"  [{violation.Kind}] {where}{frameworks}{pack}");
            output.WriteLine($"    {violation.Message}");
            if (violation.Because.Length > 0)
            {
                output.WriteLine($"    Rule {violation.RuleId}: {violation.Because}");
            }
        }
    }
}
