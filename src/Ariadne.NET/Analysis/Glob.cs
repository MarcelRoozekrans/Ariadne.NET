using ZeroAlloc.Results;

namespace Ariadne.NET.Analysis;

/// <summary>
/// An ordinal glob, case-sensitive unless compiled to ignore case, over namespaces, type names or assembly names. Built once, matched without allocating.
/// </summary>
internal sealed class Glob
{
    private const string AnySegments = "**";

    private readonly string[] _segments;
    private readonly bool _dotted;
    private readonly bool _ignoreCase;

    private Glob(string[] segments, bool dotted, bool ignoreCase)
    {
        _segments = segments;
        _dotted = dotted;
        _ignoreCase = ignoreCase;
    }

    /// <summary>
    /// Compiles a dotted pattern. A segment that is exactly <c>**</c> matches zero or more segments and <c>*</c> inside
    /// a segment matches any run of characters within that segment, possibly an empty one. Consecutive <c>**</c>
    /// segments collapse into one. Malformed names (leading, trailing or doubled dots) are not rejected when matching.
    /// </summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <returns>The glob, or a message for the loader when the pattern is malformed.</returns>
    public static Result<Glob, string> Dotted(string pattern) => Dotted(pattern, ignoreCase: false);

    /// <summary>Compiles a dotted pattern like <see cref="Dotted(string)"/>, matching characters without regard to case when asked to.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <param name="ignoreCase">Whether letters match whatever their case, as NuGet package ids do.</param>
    /// <returns>The glob, or a message for the loader when the pattern is malformed.</returns>
    public static Result<Glob, string> Dotted(string pattern, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length == 0)
        {
            return Result<Glob, string>.Failure("pattern must not be empty");
        }

        var split = pattern.Split('.');
        var segments = new List<string>(split.Length);
        foreach (var segment in split)
        {
            if (segment.Length == 0)
            {
                return Result<Glob, string>.Failure($"pattern '{pattern}' has an empty segment");
            }

            if (!string.Equals(segment, AnySegments, StringComparison.Ordinal) && segment.Contains(AnySegments, StringComparison.Ordinal))
            {
                return Result<Glob, string>.Failure($"'**' must be a whole segment in '{pattern}'");
            }

            var isAny = string.Equals(segment, AnySegments, StringComparison.Ordinal);
            if (isAny && segments.Count > 0 && string.Equals(segments[^1], AnySegments, StringComparison.Ordinal))
            {
                continue;
            }

            segments.Add(segment);
        }

        return Result<Glob, string>.Success(new Glob([.. segments], dotted: true, ignoreCase));
    }

    /// <summary>Compiles a flat pattern, in which <c>*</c> matches any run of characters, dots included.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <returns>The glob, or a message for the loader when the pattern is empty.</returns>
    public static Result<Glob, string> Flat(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return pattern.Length == 0
            ? Result<Glob, string>.Failure("pattern must not be empty")
            : Result<Glob, string>.Success(new Glob([pattern], dotted: false, ignoreCase: false));
    }

    /// <summary>
    /// Whether the glob matches the name. The empty name is the global namespace and matches only <c>**</c>.
    /// Malformed names (leading, trailing or doubled dots) are not rejected; a <c>*</c> may match an empty segment.
    /// </summary>
    /// <param name="name">The namespace, type name or assembly name.</param>
    /// <returns>True when the whole name matches.</returns>
    public bool IsMatch(ReadOnlySpan<char> name) =>
        _dotted
            ? MatchSegments(0, name, name.Length == 0 ? -1 : 0)
            : MatchWithin(_segments[0], name, _ignoreCase);

    // position is the start of the next unconsumed name segment, or -1 when the name is exhausted.
    private bool MatchSegments(int index, ReadOnlySpan<char> name, int position)
    {
        while (true)
        {
            if (index == _segments.Length)
            {
                return position < 0;
            }

            var pattern = _segments[index];
            if (string.Equals(pattern, AnySegments, StringComparison.Ordinal))
            {
                if (MatchSegments(index + 1, name, position))
                {
                    return true;
                }

                if (position < 0)
                {
                    return false;
                }

                var skipped = SegmentEnd(name, position);
                position = skipped == name.Length ? -1 : skipped + 1;
                continue;
            }

            if (position < 0)
            {
                return false;
            }

            var end = SegmentEnd(name, position);
            if (!MatchWithin(pattern, name[position..end], _ignoreCase))
            {
                return false;
            }

            position = end == name.Length ? -1 : end + 1;
            index++;
        }
    }

    private static int SegmentEnd(ReadOnlySpan<char> name, int position)
    {
        var end = position;
        while (end < name.Length && name[end] != '.')
        {
            end++;
        }

        return end;
    }

    private static bool Same(char pattern, char text, bool ignoreCase) =>
        pattern == text || (ignoreCase && char.ToUpperInvariant(pattern) == char.ToUpperInvariant(text));

    // Classic star matching: remember the last star and retry from one character further on a mismatch.
    private static bool MatchWithin(string pattern, ReadOnlySpan<char> text, bool ignoreCase)
    {
        var p = 0;
        var t = 0;
        var star = -1;
        var resume = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                resume = t;
            }
            else if (p < pattern.Length && Same(pattern[p], text[t], ignoreCase))
            {
                p++;
                t++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++resume;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
