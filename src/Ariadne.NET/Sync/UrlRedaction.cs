using System.Text.RegularExpressions;

namespace Ariadne.NET.Sync;

/// <summary>Hides credentials in urls before they reach a message: <c>https://token@host/x</c> becomes <c>https://***@host/x</c>.</summary>
internal static partial class UrlRedaction
{
    public static string Redact(string? text) =>
        string.IsNullOrEmpty(text) ? text ?? string.Empty : UserInfo().Replace(text, "://***@");

    // The userinfo of a url: everything from "://" to the last "@" before the first "/", "?" or "#", so a raw "@"
    // in a password is hidden too. Whitespace ends a url in free text such as git's stderr.
    [GeneratedRegex(@"://[^/?#\s]*@", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UserInfo();
}
