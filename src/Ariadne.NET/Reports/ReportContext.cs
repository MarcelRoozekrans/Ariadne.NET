namespace Ariadne.NET.Reports;

/// <summary>What a report writer needs besides the report itself.</summary>
/// <param name="Root">The directory violation file paths are made relative to.</param>
/// <param name="ToolVersion">The version of Ariadne.NET that produced the report.</param>
public sealed record ReportContext(string Root, string ToolVersion);
