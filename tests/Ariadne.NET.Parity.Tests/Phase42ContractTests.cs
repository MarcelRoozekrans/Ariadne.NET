using Ariadne.NET.Tests.Shared;
using FluentAssertions;

namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// Pins the names Phase 4.2 builds on. Each name is bound to a typed local, so a rename fails to compile. The assertions hold
/// before and after 4.2 closes its gaps, because 4.2 deletes its <see cref="KnownGaps"/> entries and this test still runs.
/// </summary>
[Collection(ParityFixtureCollection.Name)]
public sealed class Phase42ContractTests
{
    private static readonly string[] Phases = ["4.2", "4.3", "unplanned"];

    public Phase42ContractTests() => FixtureProjects.EnsureParityBuilt();

    [Fact]
    public void Names_phase_4_2_builds_on_exist()
    {
        IReadOnlyList<ParityCase> all = ParityCases.All;
        Func<string, ParityCase> get = ParityCases.Get;
        IReadOnlyList<KnownGap> gaps = KnownGaps.Entries;
        IReadOnlyList<Divergence> divergences = Divergences.Entries;
        Func<string, string, bool> hasRule = ParityHarness.PackHasRule;
        Func<string, string> pack = ParityHarness.KindPack;
        Action<ParityCase> agrees = ParityHarness.AssertAgrees;
        Projection types = Projection.Types;

        var gap = new KnownGap("c-x", "4.2", "reason");
        (gap.CaseId, gap.Phase, gap.Reason).Should().Be(("c-x", "4.2", "reason"));
        var parityCase = new ParityCase("c-x", "CX", "Parity.CX", "PARITY-CX", types, _ => throw new NotSupportedException());
        (parityCase.Kind, parityCase.Prefix, parityCase.AriadneRuleId).Should().Be(("CX", "Parity.CX", "PARITY-CX"));
        var divergence = new Divergence(
            "c-x",
            new HashSet<string>(StringComparer.Ordinal) { "A" },
            new HashSet<string>(StringComparer.Ordinal) { "B" },
            "reason",
            "expected");
        divergence.ArchUnitNetOnly.Should().Equal("A");
        divergence.AriadneOnly.Should().Equal("B");
        (divergence.Reason, divergence.Expected).Should().Be(("reason", "expected"));
        divergences.Should().OnlyContain(d => all.Any(c => string.Equals(c.Id, d.CaseId, StringComparison.Ordinal)));
        agrees.Should().NotBeNull();

        all.Should().NotBeEmpty();
        foreach (var g in gaps)
        {
            Phases.Should().Contain(g.Phase, "gap {0} must name a known phase", g.CaseId);
            get(g.CaseId).Id.Should().Be(g.CaseId);
        }

        var gapIds = gaps.Select(g => g.CaseId).ToHashSet(StringComparer.Ordinal);
        foreach (var c in all)
        {
            pack(c.Kind).Replace('\\', '/').Should().EndWith("rules.yaml");
            var isGap = gapIds.Contains(c.Id);
            hasRule(c.Kind, c.AriadneRuleId).Should().Be(!isGap, "case {0} has a pack rule exactly when it is no known gap", c.Id);
        }

        typeof(CatalogueConsistencyTests).Should().NotBeNull();
    }
}
