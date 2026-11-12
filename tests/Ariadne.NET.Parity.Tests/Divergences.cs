namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// The cases where the tools disagree on purpose. The parity theory asserts they disagree on exactly the recorded names, so
/// an entry cannot outlive the difference it records.
/// </summary>
public static class Divergences
{
    /// <summary>All divergence entries.</summary>
    public static IReadOnlyList<Divergence> Entries { get; } =
    [
        new(
            "c-be-kind",
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "Parity.CBeKind.Broken.Ledger" },
            "A record class compiles to a class, so ArchUnitNET's Classes() holds it. Ariadne reads source, where record is its "
            + "own kind, so 'kind: class' rejects a record.",
            "ArchUnitNET's: a record class is a class in C#. Phase 4.2's 'is: { kinds: [class, record] }' expresses Be(Classes()) "
            + "exactly; today's 'kind' takes one kind."),
        new(
            "c-only-depend-on",
            new HashSet<string>(StringComparer.Ordinal) { "Parity.COnlyDependOn.Broken.AuditTrail" },
            new HashSet<string>(StringComparer.Ordinal),
            "OnlyDependOn fails a dependency on any type of the loaded assemblies outside the allowed set, while the layer check "
            + "is open-world: a dependency on a type no layer covers, here Helpers.Clock, is not checked. Framework types count "
            + "for neither tool.",
            "ArchUnitNET's, for a developer who wrote OnlyDependOn. In a pack, cover the namespace with a layer that is not in "
            + "allowedDependencies, as the fixture does for Forbidden, and Ariadne flags the dependency too."),
    ];
}
