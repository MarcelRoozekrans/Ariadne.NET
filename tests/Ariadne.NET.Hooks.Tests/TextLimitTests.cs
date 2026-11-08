using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class TextLimitTests
{
    [Fact]
    public void Text_within_the_limit_is_returned_unchanged() => TextLimit.Shorten("abc", 3).Should().Be("abc");

    [Fact]
    public void Longer_text_ends_in_an_ellipsis_within_the_limit() => TextLimit.Shorten("abcdef", 4).Should().Be("abc…");

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void A_limit_of_zero_or_less_gives_an_empty_string(int max) => TextLimit.Shorten("abc", max).Should().BeEmpty();

    [Fact]
    public void A_limit_of_one_gives_only_the_ellipsis() => TextLimit.Shorten("abc", 1).Should().Be("…");

    [Fact]
    public void The_cut_never_splits_a_surrogate_pair()
    {
        // "ab" + U+1F600 (two chars) + "cd": a cut after two kept chars plus the pair half would leave the high surrogate alone.
        var shortened = TextLimit.Shorten("ab\U0001F600cd", 4);

        shortened.Should().Be("ab…");
        shortened.Should().NotContain("\uD83D");
    }

    [Fact]
    public void A_pair_that_fits_before_the_cut_is_kept() => TextLimit.Shorten("a\U0001F600cdef", 5).Should().Be("a\U0001F600c…");

    [Fact]
    public void Null_text_is_refused()
    {
        var act = () => TextLimit.Shorten(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
