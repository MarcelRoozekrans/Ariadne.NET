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
        new(
            "s-free-of-cycles",
            "4.3",
            "No slices rule: a pack cannot group types into slices by a namespace capture or forbid cycles between them."),
        new(
            "s-not-depend-on-each-other",
            "4.3",
            "No slices rule: a pack cannot group types into slices by a namespace capture or keep slices independent."),
        new(
            "s-matching-with-packages",
            "4.3",
            "No slices rule: a pack cannot group types into slices by a namespace capture or keep slices independent."),
    ];
}
