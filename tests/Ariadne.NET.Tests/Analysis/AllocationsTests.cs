using FluentAssertions;
using Xunit;

namespace Ariadne.NET.Tests.Analysis;

public sealed class AllocationsTests
{
    private static object? _sink;

    [Fact]
    public void PerRound_reports_an_allocation_made_on_each_call()
    {
        Allocations.PerRound(() => _sink = new object()).Should().BeGreaterThanOrEqualTo(1_000 * 24);
    }

    [Fact]
    public void PerRound_is_zero_for_code_that_allocates_nothing()
    {
        var count = 0;
        Allocations.PerRound(() => count++).Should().Be(0);
        count.Should().Be(5_000);
    }
}
