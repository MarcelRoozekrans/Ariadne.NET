using FluentAssertions;

namespace Ariadne.NET.Parity.Tests;

public sealed class Phase42CompletionTests
{
    [Fact]
    public void No_known_gap_left_for_phase_4_2()
    {
        KnownGaps.Entries.Where(g => string.Equals(g.Phase, "4.2", StringComparison.Ordinal))
            .Select(g => g.CaseId).Should().BeEmpty("phase 4.2 closes every gap it planned");

        ParityCatalogue.Load(ParityCatalogue.PagePath)
            .Where(r => string.Equals(r.Status, "gap", StringComparison.Ordinal) && r.Notes.Contains("4.2", StringComparison.Ordinal))
            .Select(r => r.Id).Should().BeEmpty("no catalogue row may still be a gap noted for phase 4.2");
    }
}
