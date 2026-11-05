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
    [InlineData("a‮b", "a\\u202eb")]
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
}
