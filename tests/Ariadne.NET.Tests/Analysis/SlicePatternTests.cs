using Ariadne.NET.Analysis;
using FluentAssertions;

namespace Ariadne.NET.Tests.Analysis;

public sealed class SlicePatternTests
{
    [Theory]
    [InlineData("Shop.(*)", "Shop.Orders", "Orders")]
    [InlineData("Shop.(*)", "Shop.Orders.Core", null)]
    [InlineData("Shop.(*).**", "Shop.Orders.Core.Deep", "Orders")]
    [InlineData("Shop.(**)", "Shop.Orders.Core", "Orders.Core")]
    [InlineData("Shop.(**)", "Shop", null)]
    [InlineData("Shop.(**).Core", "Shop.Orders.Core", "Orders")]
    [InlineData("Shop.(**).Core", "Shop.Orders.Core.Deep", null)]
    [InlineData("**.(*).Core.**", "A.Orders.Core.X.Core.Y", "Orders")]
    [InlineData("**.(*).Public.**", "Public.Contracts", null)]
    [InlineData("**.(*).Public.**", "Shop.Orders.Public", "Orders")]
    [InlineData("X.(**).**", "X.A.B", "A")]
    [InlineData("**.(*)", "Orders", "Orders")]
    [InlineData("(**)", "", null)]
    [InlineData("Shop.(*)", "shop.Orders", null)]
    public void Captures(string pattern, string ns, string? expected)
    {
        var slicePattern = SlicePattern.Compile(pattern).Value;

        var hit = slicePattern.TryCapture(ns, out var slice);

        if (expected is null)
        {
            hit.Should().BeFalse();
        }
        else
        {
            hit.Should().BeTrue();
            slice.Should().Be(expected);
        }
    }

    [Theory]
    [InlineData("Shop.Orders", "exactly one capture")]
    [InlineData("(*).(**)", "exactly one capture")]
    [InlineData("Shop.Ord(*)", "whole segment")]
    [InlineData("Shop..(*)", "empty segment")]
    [InlineData("", "must not be empty")]
    [InlineData("Shop.Or**.(*)", "'**' must be a whole segment")]
    public void Invalid(string pattern, string fragment)
    {
        SlicePattern.Validate(pattern).Should().Contain(fragment);
        var compiled = SlicePattern.Compile(pattern);
        compiled.IsFailure.Should().BeTrue();
        compiled.Error.Should().Be(SlicePattern.Validate(pattern));
    }

    [Theory]
    [InlineData("Shop.(*)")]
    [InlineData("**.(**).Core.**")]
    public void Valid_pattern_has_no_message(string pattern) => SlicePattern.Validate(pattern).Should().BeNull();

    // Interim: measures with GC.GetAllocatedBytesForCurrentThread, as GlobTests does.
    [Fact]
    public void TryCapture_does_not_allocate_on_a_miss()
    {
        var pattern = SlicePattern.Compile("**.(**).Core.**").Value;
        pattern.TryCapture("Shop.Orders.Public.Deep", out _).Should().BeFalse();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
        {
            pattern.TryCapture("Shop.Orders.Public.Deep", out _);
        }

        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
    }
}
