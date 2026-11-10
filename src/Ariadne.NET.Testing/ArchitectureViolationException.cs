using System.Globalization;
using Ariadne.NET.Analysis;
using Ariadne.NET.Reports;

namespace Ariadne.NET.Testing;

/// <summary>
/// Thrown by <see cref="ArchitectureCheck.AssertValid"/> and <see cref="ArchitectureCheck.AssertValidAsync"/> when the solution
/// breaks a rule. Its <see cref="Exception.Message"/> is the text report <c>ariadne validate</c> prints, with each violation's
/// location, pack, rule id and <c>because</c>.
/// </summary>
public sealed class ArchitectureViolationException : ArchitectureCheckException
{
    /// <summary>Creates the exception with a default message and an empty <see cref="Report"/>.</summary>
    public ArchitectureViolationException()
    {
        Report = EmptyReport();
    }

    /// <summary>Creates the exception with the given message and an empty <see cref="Report"/>.</summary>
    /// <param name="message">What went wrong.</param>
    public ArchitectureViolationException(string message)
        : base(message)
    {
        Report = EmptyReport();
    }

    /// <summary>Creates the exception with the given message, the exception that caused it and an empty <see cref="Report"/>.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public ArchitectureViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Report = EmptyReport();
    }

    /// <summary>Creates the exception for a report with violations.</summary>
    /// <param name="report">The report of the failed check.</param>
    /// <param name="root">The folder that violation file paths in the message are made relative to.</param>
    internal ArchitectureViolationException(ArchitectureReport report, string root)
        : base(Render(report, root))
    {
        Report = report;
    }

    /// <summary>The report of the check, with every violation found; empty when the exception was made without one.</summary>
    public ArchitectureReport Report { get; }

    private static ArchitectureReport EmptyReport() => new() { Violations = [], Projects = [], TypesChecked = 0 };

    private static string Render(ArchitectureReport report, string root)
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        TextReportWriter.Write(text, report, root);
        return text.ToString().TrimEnd();
    }
}
