using Ariadne.NET.Tests.Shared;
using FluentAssertions;

namespace Ariadne.NET.Parity.Tests;

// Both theories run over every case id and return early for a case that is not a gap: xUnit 2 fails a theory whose member
// data is empty, and KnownGaps.Entries may be.
[Collection(ParityFixtureCollection.Name)]
public sealed class KnownGapTests
{
    public KnownGapTests() => FixtureProjects.EnsureParityBuilt();

    [Theory]
    [MemberData(nameof(ParityCases.Ids), MemberType = typeof(ParityCases))]
    public void Gap_fixture_is_proven(string caseId)
    {
        if (!ParityHarness.IsKnownGap(caseId))
        {
            return;
        }

        var c = ParityCases.Get(caseId);
        var failures = ParityHarness.ArchUnitNetFailures(c);

        failures.Should().NotBeEmpty("ArchUnitNET must flag the broken fixture of gap case '{0}'", caseId);
        failures.Should().OnlyContain(
            name => ParityHarness.IsIn(name, c.BrokenNamespace),
            "ArchUnitNET may flag only the {0} namespace of gap case '{1}'", c.BrokenNamespace, caseId);
    }

    [Theory]
    [MemberData(nameof(ParityCases.Ids), MemberType = typeof(ParityCases))]
    public void Gap_has_no_ariadne_rule(string caseId)
    {
        if (!ParityHarness.IsKnownGap(caseId))
        {
            return;
        }

        ParityHarness.AssertGapHasNoRule(ParityCases.Get(caseId));
    }
}
