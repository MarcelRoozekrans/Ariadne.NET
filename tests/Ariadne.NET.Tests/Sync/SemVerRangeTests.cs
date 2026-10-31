using FluentAssertions;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Sync;

namespace Ariadne.NET.Tests.Sync;

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
        var semVerRange = SemVerRange.Parse(range).Value;
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
        var semVerRange = SemVerRange.Parse(range).Value;
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
        var semVerRange = SemVerRange.Parse(range).Value;
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
        var semVerRange = SemVerRange.Parse(range).Value;
        var tags = new[] { t1, t2, t3, t4 };

        var best = semVerRange.FindBestMatch(tags);

        best.Should().Be(expectedTag);
    }

    [Fact]
    public void FindBestMatch_NoMatch_ShouldReturnNull()
    {
        var semVerRange = SemVerRange.Parse("^1.0").Value;
        var tags = new[] { "v3.0.0", "v4.0.0" };

        var best = semVerRange.FindBestMatch(tags);

        best.Should().BeNull();
    }

    [Theory]
    [InlineData("^2.0")]
    [InlineData("~1.3")]
    [InlineData("1.0.0")]
    public void Parse_ValidRanges_ReturnsSuccess(string range)
    {
        SemVerRange.Parse(range).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-valid")]
    [InlineData("^nope")]
    [InlineData("~nope")]
    public void Parse_InvalidRanges_ReturnsInvalid(string range)
    {
        var result = SemVerRange.Parse(range);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be($"Version range '{range}' is not a valid semver range.");
    }

    [Theory]
    [InlineData("v1.0.0-beta")]
    [InlineData("v1.0.0-rc.1")]
    [InlineData("v2.0.0-alpha+build.123")]
    [InlineData("1.0.0-pre")]
    public void TryParse_PreReleaseTags_ShouldBeRejected(string input)
    {
        SemVer.TryParse(input, out _).Should().BeFalse();
    }

    [Fact]
    public void FindBestMatch_ShouldIgnorePreReleaseTags()
    {
        var tags = new[] { "v1.0.0-beta", "v1.0.0", "v1.1.0-rc.1", "v1.1.0" };
        var range = SemVerRange.Parse("^1.0").Value;

        var best = range.FindBestMatch(tags);

        best.Should().Be("v1.1.0");
    }

    [Fact]
    public void FindBestMatch_OnlyPreReleaseTags_ShouldReturnNull()
    {
        var tags = new[] { "v1.0.0-beta", "v1.1.0-rc.1", "v2.0.0-alpha" };
        var range = SemVerRange.Parse("^1.0").Value;

        range.FindBestMatch(tags).Should().BeNull();
    }

    [Theory]
    [InlineData("^99999999999.0.0")]
    [InlineData("~1.99999999999")]
    [InlineData("1.0.99999999999")]
    [InlineData("^١.٠.٠")]
    public void Parse_OutOfRangeOrNonAsciiNumbers_ReturnsInvalid(string range)
    {
        var result = SemVerRange.Parse(range);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("v99999999999.0.0")]
    [InlineData("v1.2147483648.0")]
    [InlineData("v١.٠.٠")]
    public void TryParse_NullOverflowOrNonAsciiDigits_ReturnsFalse(string? input)
    {
        SemVer.TryParse(input, out var version).Should().BeFalse();
        version.Should().BeNull();
    }

    [Fact]
    public void TryParse_IntMaxValue_StillParses()
    {
        SemVer.TryParse("v2147483647.0.0", out var version).Should().BeTrue();
        version!.Major.Should().Be(int.MaxValue);
    }
}
