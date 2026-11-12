using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Ariadne.NET.Parity.Tests;

/// <summary>Every parity case: one per in-scope catalogue row and polarity.</summary>
public static class ParityCases
{
    /// <summary>All cases, in catalogue order.</summary>
    public static IReadOnlyList<ParityCase> All { get; } =
    [
        new(
            "c-not-depend-on-any",
            "CNotDependOnAny",
            "Parity.CNotDependOnAny",
            "PARITY-CNOTDEPENDONANY",
            Projection.Types,
            _ => Types().That().ResideInNamespace("Parity.CNotDependOnAny.Broken")
                .Or().ResideInNamespace("Parity.CNotDependOnAny.Clean")
                .Should().NotDependOnAny(Types().That().ResideInNamespace("Parity.CNotDependOnAny.Target"))),
    ];

    /// <summary>The case with <paramref name="id"/>.</summary>
    /// <exception cref="KeyNotFoundException">No case has that id.</exception>
    public static ParityCase Get(string id) =>
        All.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"No parity case has the id '{id}'.");

    /// <summary>Every case id, as xUnit member data.</summary>
    public static IEnumerable<object[]> Ids() => All.Select(c => new object[] { c.Id });
}
