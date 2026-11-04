using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Tests.Analysis;

/// <summary>A syntax tree options provider that reports one tree as marked generated, as an analyzer config would.</summary>
internal sealed class MarkedGeneratedOptionsProvider(SyntaxTree? generatedTree) : SyntaxTreeOptionsProvider
{
    public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken) =>
        tree == generatedTree ? GeneratedKind.MarkedGenerated : GeneratedKind.NotGenerated;

    public override bool TryGetDiagnosticValue(SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
    {
        severity = ReportDiagnostic.Default;
        return false;
    }

    public override bool TryGetGlobalDiagnosticValue(string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
    {
        severity = ReportDiagnostic.Default;
        return false;
    }
}
