using System.Buffers;
using Ariadne.NET.Analysis;
using Ariadne.NET.Reports.Sarif;

namespace Ariadne.NET.Reports;

/// <summary>Writes an <see cref="ArchitectureReport"/> as a SARIF 2.1.0 log.</summary>
public static class SarifReportWriter
{
    private const string SchemaUri = "https://json.schemastore.org/sarif-2.1.0.json";
    private const string SrcRoot = "%SRCROOT%";
    private const string InformationUri = "https://marcelroozekrans.github.io/Ariadne.NET/";

    private static readonly SarifLogSerializer Serializer = new();

    /// <summary>
    /// Writes the report as compact UTF-8 SARIF without a byte order mark: one run, one rule per distinct rule id and pack,
    /// one result per violation. File locations are relative to <see cref="ReportContext.Root"/> through the <c>%SRCROOT%</c>
    /// base; a file outside the root gets an absolute <c>file://</c> uri instead.
    /// </summary>
    /// <param name="output">Where the SARIF goes.</param>
    /// <param name="report">The report to write.</param>
    /// <param name="context">The root violation paths are made relative to, and the tool version.</param>
    public static void Write(Stream output, ArchitectureReport report, ReportContext context)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);

        var rules = new List<SarifRule>();
        var indexes = new Dictionary<(string RuleId, string Pack), int>();
        var results = new List<SarifResult>(report.Violations.Count);
        foreach (var violation in report.Violations)
        {
            var key = (violation.RuleId, violation.Pack);
            if (!indexes.TryGetValue(key, out var index))
            {
                index = rules.Count;
                indexes[key] = index;
                rules.Add(new SarifRule(
                    violation.RuleId,
                    new SarifMessage(violation.Kind.ToString()),
                    new SarifMessage(violation.Because),
                    new SarifRuleProperties(violation.Pack)));
            }

            results.Add(new SarifResult(
                violation.RuleId,
                index,
                "error",
                new SarifMessage(violation.Message),
                [ToLocation(violation, context.Root)],
                new SarifResultProperties(violation.Pack, violation.TargetFrameworks)));
        }

        var log = new SarifLog(
            SchemaUri,
            "2.1.0",
            [
                new SarifRun(
                    new SarifTool(new SarifDriver("Ariadne.NET", context.ToolVersion, InformationUri, rules)),
                    new Dictionary<string, SarifUriBase>(StringComparer.Ordinal) { [SrcRoot] = new SarifUriBase(RootUri(context.Root)) },
                    results),
            ]);

        var buffer = new ArrayBufferWriter<byte>();
        Serializer.Serialize(buffer, log);
        output.Write(buffer.WrittenSpan);
    }

    private static SarifLocation ToLocation(ArchitectureViolation violation, string root)
    {
        if (violation.Location is not { } location)
        {
            // A type for the kinds whose source is a type; a rule id, a layer name or a project name is not one.
            var kind = violation.Kind is ViolationKind.VacuousRule or ViolationKind.VacuousLayer or ViolationKind.BannedPackage ? null : "type";
            return new SarifLocation(null, [new SarifLogicalLocation(violation.SourceType, kind)]);
        }

        var uri = ReportPaths.TryRelative(root, location.FilePath, out var relative)
            ? new SarifArtifactLocation(string.Join('/', relative.Split('/').Select(Uri.EscapeDataString)), SrcRoot)
            : new SarifArtifactLocation(ReportPaths.ToFileUri(Path.GetFullPath(location.FilePath)), null);
        return new SarifLocation(new SarifPhysicalLocation(uri, new SarifRegion(location.Line, location.Column)), null);
    }

    private static string RootUri(string root)
    {
        var full = Path.GetFullPath(root);
        if (!full.EndsWith(Path.DirectorySeparatorChar) && !full.EndsWith(Path.AltDirectorySeparatorChar))
        {
            full += Path.DirectorySeparatorChar;
        }

        return ReportPaths.ToFileUri(full);
    }
}
