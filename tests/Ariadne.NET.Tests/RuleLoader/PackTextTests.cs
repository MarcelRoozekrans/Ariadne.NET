using Ariadne.NET.RuleLoader;
using FluentAssertions;

namespace Ariadne.NET.Tests.RuleLoader;

public class PackTextTests
{
    [Fact]
    public void ShortPrintableText_IsReturnedAsIs()
    {
        var text = "Shop.Domain.**";

        PackText.Show(text).Should().BeSameAs(text);
    }

    [Fact]
    public void TextOfExactlyTheLimit_IsNotCut()
    {
        var text = new string('a', PackText.MaxLength);

        PackText.Show(text).Should().Be(text);
    }

    [Fact]
    public void TextOverTheLimit_IsCutWithDots()
    {
        PackText.Show(new string('a', PackText.MaxLength + 1)).Should().Be(new string('a', PackText.MaxLength) + "...");
    }

    [Theory]
    [InlineData("a\u001bb", "a\\u001bb")]
    [InlineData("a\nb", "a\\u000ab")]
    [InlineData("a\u202Eb", "a\\u202eb")]
    [InlineData("a\u2028b", "a\\u2028b")]
    [InlineData("a\u2029b", "a\\u2029b")]
    public void ControlAndFormatCharacters_AreEscaped(string text, string expected)
    {
        PackText.Show(text).Should().Be(expected);
    }

    [Fact]
    public void Cut_DoesNotSplitASurrogatePair()
    {
        var text = new string('a', PackText.MaxLength - 1) + "\U0001F600" + "tail";

        PackText.Show(text).Should().Be(new string('a', PackText.MaxLength - 1) + "...");
    }

    [Fact]
    public void Escape_DoesNotCut()
    {
        var text = new string('a', 200) + "\u001b";

        PackText.Escape(text).Should().Be(new string('a', 200) + "\\u001b");
    }

    [Fact]
    public void SurrogatePair_IsKeptAsIs()
    {
        var text = "a\U0001F600b";

        PackText.Show(text).Should().BeSameAs(text);
    }

    // Built in code, not as InlineData: xUnit does not carry a lone surrogate through test-case serialisation intact.
    [Fact]
    public void LoneSurrogates_AreEscaped()
    {
        PackText.Show("a" + '\uD800' + "b").Should().Be("a\\ud800b");
        PackText.Show("a" + '\uDC00' + "b").Should().Be("a\\udc00b");
        PackText.Show("a" + '\uD800').Should().Be("a\\ud800");
        PackText.Show(string.Concat("a", '\uDC00', '\uD800', "b")).Should().Be("a\\udc00\\ud800b");
    }
}
