namespace Ariadne.NET.Parity.Tests;

/// <summary>A case where the two tools disagree on purpose.</summary>
/// <param name="CaseId">The parity case id.</param>
/// <param name="ArchUnitNetOnly">The canonical names only ArchUnitNET flags.</param>
/// <param name="AriadneOnly">The canonical names only Ariadne flags.</param>
/// <param name="Reason">Why the tools disagree.</param>
/// <param name="Expected">Which tool's answer a developer would expect, and why.</param>
public sealed record Divergence(
    string CaseId,
    IReadOnlySet<string> ArchUnitNetOnly,
    IReadOnlySet<string> AriadneOnly,
    string Reason,
    string Expected);
