namespace Ariadne.NET.Reports;

/// <summary>A machine-readable report format.</summary>
public enum ReportFormat
{
    /// <summary>Ariadne's own versioned JSON.</summary>
    Json,

    /// <summary>SARIF 2.1.0.</summary>
    Sarif,
}
