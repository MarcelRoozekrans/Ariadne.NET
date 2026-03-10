using FluentAssertions;
using ArchGov.Core.Sync;

namespace ArchGov.Core.Tests.Sync;

public class SemVerRangeTests
{
    [Theory]
    [InlineData("v1.0.0", 1, 0, 0)]
    [InlineData("1.0.0", 1, 0, 0)]
    [InlineData("v2.3.1", 2, 3, 1)]
    [InlineData("0.1.0", 0, 1, 0)]
    public void TryParse_ValidVersions_ShouldParse(string input, int major, int minor, int patch)
    {
        var result = SemVer.TryParse(input, out var version);

        result.Should().BeTrue();
        version.Should().NotBeNull();
        version!.Major.Should().Be(major);
        version.Minor.Should().Be(minor);
        version.Patch.Should().Be(patch);
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("latest")]
    [InlineData("refs/tags/foo")]
    public void TryParse_InvalidVersions_ShouldReturnFalse(string input)
    {
        var result = SemVer.TryParse(input, out var version);

        result.Should().BeFalse();
        version.Should().BeNull();
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "2.0.0", false)]
    public void ExactMatch_ShouldMatchExactly(string range, string candidate, bool expected)
    {
        var semVerRange = SemVerRange.Parse(range);
        SemVer.TryParse(candidate, out var version);

        semVerRange.IsSatisfiedBy(version!).Should().Be(expected);
    }

    [Theory]
    [InlineData("^1.0.0", "1.0.0", true)]
    [InlineData("^1.0.0", "1.9.9", true)]
    [InlineData("^1.0.0", "2.0.0", false)]
    [InlineData("^1.2.0", "1.2.0", true)]
    [InlineData("^1.2.0", "1.3.0", true)]
    [InlineData("^1.2.0", "1.1.0", false)]
    [InlineData("^0.1.0", "0.1.0", true)]
    [InlineData("^0.1.0", "0.1.5", true)]
    [InlineData("^0.1.0", "0.2.0", false)]
    public void CaretRange_ShouldAllowMinorAndPatchBumps(string range, string candidate, bool expected)
    {
        var semVerRange = SemVerRange.Parse(range);
        SemVer.TryParse(candidate, out var version);

        semVerRange.IsSatisfiedBy(version!).Should().Be(expected);
    }

    [Theory]
    [InlineData("~1.2.0", "1.2.0", true)]
    [InlineData("~1.2.0", "1.2.9", true)]
    [InlineData("~1.2.0", "1.3.0", false)]
    [InlineData("~1.2.0", "1.1.0", false)]
    public void TildeRange_ShouldAllowPatchBumpsOnly(string range, string candidate, bool expected)
    {
        var semVerRange = SemVerRange.Parse(range);
        SemVer.TryParse(candidate, out var version);

        semVerRange.IsSatisfiedBy(version!).Should().Be(expected);
    }

    [Theory]
    [InlineData("^2.0", "v2.0.0", "v2.1.0", "v2.4.1", "v3.0.0", "v2.4.1")]
    [InlineData("~1.2", "v1.2.0", "v1.2.3", "v1.3.0", "v1.1.0", "v1.2.3")]
    [InlineData("1.0.0", "v1.0.0", "v1.0.1", "v2.0.0", "v0.9.0", "v1.0.0")]
    public void FindBestMatch_ShouldReturnHighestSatisfying(
        string range, string t1, string t2, string t3, string t4, string expectedTag)
    {
        var semVerRange = SemVerRange.Parse(range);
        var tags = new[] { t1, t2, t3, t4 };

        var best = semVerRange.FindBestMatch(tags);

        best.Should().Be(expectedTag);
    }

    [Fact]
    public void FindBestMatch_NoMatch_ShouldReturnNull()
    {
        var semVerRange = SemVerRange.Parse("^1.0");
        var tags = new[] { "v3.0.0", "v4.0.0" };

        var best = semVerRange.FindBestMatch(tags);

        best.Should().BeNull();
    }

    [Theory]
    [InlineData("^2.0")]
    [InlineData("~1.3")]
    [InlineData("1.0.0")]
    public void Parse_ValidRanges_ShouldNotThrow(string range)
    {
        var act = () => SemVerRange.Parse(range);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-valid")]
    public void Parse_InvalidRanges_ShouldThrow(string range)
    {
        var act = () => SemVerRange.Parse(range);

        act.Should().Throw<FormatException>();
    }
}
