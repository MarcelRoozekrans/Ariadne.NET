namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// The cases Ariadne cannot express yet. Each gap case's fixture must still fail ArchUnitNET's rule, and its pack must not
/// hold the case's rule, so an entry cannot outlive the gap it records.
/// </summary>
public static class KnownGaps
{
    /// <summary>All gap entries.</summary>
    public static IReadOnlyList<KnownGap> Entries { get; } =
    [
        new(
            "p-have-any-attributes:not",
            "unplanned",
            "No 'attributes' in rule exceptions, so an exception cannot leave out the types that carry an attribute; phase "
            + "4.2 adds only the eight traits there. Tracked in #97."),
    ];
}
