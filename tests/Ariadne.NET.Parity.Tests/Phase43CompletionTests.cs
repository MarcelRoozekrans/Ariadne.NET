using FluentAssertions;

namespace Ariadne.NET.Parity.Tests;

public sealed class Phase43CompletionTests
{
    private static readonly HashSet<string> Closed = new(StringComparer.Ordinal) { "covered", "divergence" };

    [Fact]
    public void No_known_gap_left_for_phase_4_3()
    {
        KnownGaps.Entries.Where(g => string.Equals(g.Phase, "4.3", StringComparison.Ordinal))
            .Select(g => g.CaseId).Should().BeEmpty("phase 4.3 closes every gap it planned");

        var sliceRows = ParityCatalogue.Load(ParityCatalogue.PagePath)
            .Where(r => r.Id.StartsWith("s-", StringComparison.Ordinal))
            .ToList();

        sliceRows.Should().NotBeEmpty("the catalogue lists the slice rows");
        sliceRows.Where(r => !Closed.Contains(r.Status))
            .Select(r => r.Id).Should().BeEmpty("every slice row is covered or a divergence after phase 4.3");
    }
}
