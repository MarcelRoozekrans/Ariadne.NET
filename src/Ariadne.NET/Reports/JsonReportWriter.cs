using System.Buffers;
using Ariadne.NET.Analysis;
using Ariadne.NET.Reports.Json;

namespace Ariadne.NET.Reports;

/// <summary>Writes an <see cref="ArchitectureReport"/> as Ariadne's own versioned JSON.</summary>
public static class JsonReportWriter
{
    private const int SchemaVersion = 1;

    private static readonly JsonReportDocumentSerializer Serializer = new();

    /// <summary>
    /// Writes the report as compact UTF-8 JSON without a byte order mark. Violation paths are relative to
    /// <see cref="ReportContext.Root"/> with <c>/</c> separators; a path outside the root is kept as given, with <c>/</c> separators.
    /// </summary>
    /// <param name="output">Where the JSON goes.</param>
    /// <param name="report">The report to write.</param>
    /// <param name="context">The root violation paths are made relative to, and the tool version.</param>
    public static void Write(Stream output, ArchitectureReport report, ReportContext context)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(context);

        var document = new JsonReportDocument(
            SchemaVersion,
            new JsonTool("ariadne", context.ToolVersion),
            report.TypesChecked,
            [.. report.Projects.Select(p => new JsonProject(p.Name, p.TargetFrameworks))],
            [.. report.SkippedProjects.Select(p => new JsonSkippedProject(p.Name, p.Reason))],
            [.. report.Violations.Select(v => ToJson(v, context.Root))]);

        var buffer = new ArrayBufferWriter<byte>();
        Serializer.Serialize(buffer, document);
        output.Write(buffer.WrittenSpan);
    }

    private static JsonViolation ToJson(ArchitectureViolation violation, string root) => new(
        violation.RuleId,
        violation.Pack,
        violation.Kind.ToString(),
        violation.SourceType,
        violation.Message,
        violation.Because,
        violation.Target,
        violation.Location is { } location ? new JsonLocation(RelativePath(root, location.FilePath), location.Line, location.Column) : null,
        violation.ReferenceCount,
        violation.TargetFrameworks);

    private static string RelativePath(string root, string filePath)
    {
        var relative = Path.GetRelativePath(root, filePath);
        var outside = Path.IsPathRooted(relative)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith("../", StringComparison.Ordinal)
            || relative.StartsWith("..\\", StringComparison.Ordinal);
        return (outside ? filePath : relative).Replace('\\', '/');
    }
}
