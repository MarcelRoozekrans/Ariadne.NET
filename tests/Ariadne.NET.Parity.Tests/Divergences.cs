namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// The cases where the tools disagree on purpose. The parity theory asserts they disagree on exactly the recorded names, so
/// an entry cannot outlive the difference it records.
/// </summary>
public static class Divergences
{
    /// <summary>All divergence entries.</summary>
    public static IReadOnlyList<Divergence> Entries { get; } = [];
}
