namespace Ariadne.NET.Hooks;

/// <summary>Bounds text that comes from outside the hook, such as error messages and import names.</summary>
internal static class TextLimit
{
    /// <summary>The longest single line the session context keeps.</summary>
    internal const int MaxLine = 500;

    private const string Ellipsis = "…";

    /// <summary>Returns <paramref name="text"/> cut to at most <paramref name="max"/> chars, ending in an ellipsis, without splitting a surrogate pair.</summary>
    internal static string Shorten(string text, int max = MaxLine)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= max)
        {
            return text;
        }

        if (max <= 0)
        {
            return "";
        }

        if (max == 1)
        {
            return Ellipsis;
        }

        var keep = max - 1;
        if (char.IsHighSurrogate(text[keep - 1]))
        {
            keep--;
        }

        return string.Concat(text.AsSpan(0, keep), Ellipsis);
    }
}
