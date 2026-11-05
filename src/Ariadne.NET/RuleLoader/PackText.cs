using System.Globalization;
using System.Text;

namespace Ariadne.NET.RuleLoader;

/// <summary>
/// Shows text from a pack inside an error message. A pack may come from a third party, and messages end up on a
/// terminal, so control and format characters are escaped, such as ESC as <c>\u001b</c>, and long text is cut.
/// </summary>
internal static class PackText
{
    /// <summary>The most characters of pack text a message shows before it cuts it with <c>...</c>.</summary>
    public const int MaxLength = 80;

    private const string Cut = "...";

    /// <summary>The text escaped and cut to <see cref="MaxLength"/> characters. Short printable text is returned as is.</summary>
    /// <param name="value">Text from the pack, such as a key, a name or a pattern.</param>
    /// <returns>The text to put in the message.</returns>
    public static string Show(string? value) => Format(value ?? string.Empty, MaxLength);

    /// <summary>The text escaped but not cut, for a message the YAML parser wrote around pack text.</summary>
    /// <param name="value">The message.</param>
    /// <returns>The escaped message.</returns>
    public static string Escape(string value) => Format(value, int.MaxValue);

    private static string Format(string value, int maxLength)
    {
        if (value.Length <= maxLength && !NeedsEscape(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + Cut.Length);
        foreach (var c in value)
        {
            if (IsUnsafe(c))
            {
                builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(c);
            }
        }

        if (builder.Length <= maxLength)
        {
            return builder.ToString();
        }

        builder.Length = maxLength;
        if (char.IsHighSurrogate(builder[^1]))
        {
            builder.Length--;
        }

        return builder.Append(Cut).ToString();
    }

    private static bool NeedsEscape(string value)
    {
        foreach (var c in value)
        {
            if (IsUnsafe(c))
            {
                return true;
            }
        }

        return false;
    }

    // Control characters move the cursor or start escape sequences; format characters such as a right-to-left override
    // reorder what the reader sees.
    private static bool IsUnsafe(char c) =>
        char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format;
}
