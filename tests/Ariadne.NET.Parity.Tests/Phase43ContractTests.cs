using Ariadne.NET.Tests.Shared;
using FluentAssertions;

namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// Pins the names Phase 4.3 builds on. Each name is bound to a typed local, so a rename fails to compile. The assertions hold
/// before and after 4.3 closes its slice gaps.
/// </summary>
[Collection(ParityFixtureCollection.Name)]
public sealed class Phase43ContractTests
{
    private static readonly string[] SliceKinds = ["SFreeOfCycles", "SNotDependOnEachOther", "SMatchingWithPackages"];

    public Phase43ContractTests() => FixtureProjects.EnsureParityBuilt();

    [Fact]
    public void Names_phase_4_3_builds_on_exist()
    {
        IReadOnlyList<ParityCase> all = ParityCases.All;
        Func<string, ParityCase> get = ParityCases.Get;
        IReadOnlyList<KnownGap> gaps = KnownGaps.Entries;
        Func<string, string, bool> hasRule = ParityHarness.PackHasRule;
        Func<string, string> pack = ParityHarness.KindPack;
        Action<ParityCase> agrees = ParityHarness.AssertAgrees;
        Projection slices = Projection.Slices;

        var constructed = new ParityCase("s-x", "SX", "Parity.SX", "PARITY-SX", slices, _ => throw new NotSupportedException(), "Parity.SX.(**)");
        constructed.SlicePattern.Should().Be("Parity.SX.(**)");
        agrees.Should().NotBeNull();

        var gapsById = gaps.ToDictionary(g => g.CaseId, StringComparer.Ordinal);
        foreach (var kind in SliceKinds)
        {
            var c = all.Should().ContainSingle(x => string.Equals(x.Kind, kind, StringComparison.Ordinal)).Subject;
            c.Projection.Should().Be(slices);
            c.SlicePattern.Should().Be($"Parity.{kind}.(**)");
            File.Exists(pack(kind)).Should().BeTrue("kind {0} has a pack", kind);

            if (gapsById.TryGetValue(c.Id, out var gap))
            {
                gap.Phase.Should().Be("4.3");
                hasRule(kind, c.AriadneRuleId).Should().BeFalse("gap case {0} has no pack rule yet", c.Id);
            }
            else
            {
                hasRule(kind, c.AriadneRuleId).Should().BeTrue("case {0} has no gap entry, so its pack has the rule", c.Id);
            }
        }

        var cycles = all.Single(x => string.Equals(x.Kind, "SFreeOfCycles", StringComparison.Ordinal));
        ParityHarness.ArchUnitNetFailures(get(cycles.Id)).Should().BeEquivalentTo(
            ["Parity.SFreeOfCycles.Broken.Orders", "Parity.SFreeOfCycles.Broken.Billing"]);

        typeof(CatalogueConsistencyTests).Should().NotBeNull();
    }
}
