using ZeroAlloc.Results;

namespace Ariadne.NET.Analysis;

/// <summary>
/// An ordinal, case-sensitive glob over namespaces, type names or assembly names. Built once, matched without allocating.
/// </summary>
internal sealed class Glob
{
    private const string AnySegments = "**";

    private readonly string[] _segments;
    private readonly bool _dotted;

    private Glob(string[] segments, bool dotted)
    {
        _segments = segments;
        _dotted = dotted;
    }

    /// <summary>
    /// Compiles a dotted pattern. A segment that is exactly <c>**</c> matches zero or more segments and <c>*</c> inside
    /// a segment matches any run of characters within that segment.
    /// </summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <returns>The glob, or a message for the loader when the pattern is malformed.</returns>
    public static Result<Glob, string> Dotted(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length == 0)
        {
            return Result<Glob, string>.Failure("pattern must not be empty");
        }

        var segments = pattern.Split('.');
        foreach (var segment in segments)
        {
            if (segment.Length == 0)
            {
                return Result<Glob, string>.Failure($"pattern '{pattern}' has an empty segment");
            }

            if (!string.Equals(segment, AnySegments, StringComparison.Ordinal) && segment.Contains(AnySegments, StringComparison.Ordinal))
            {
                return Result<Glob, string>.Failure($"'**' must be a whole segment in '{pattern}'");
            }
        }

        return Result<Glob, string>.Success(new Glob(segments, dotted: true));
    }

    /// <summary>Compiles a flat pattern, in which <c>*</c> matches any run of characters, dots included.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <returns>The glob, or a message for the loader when the pattern is empty.</returns>
    public static Result<Glob, string> Flat(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return pattern.Length == 0
            ? Result<Glob, string>.Failure("pattern must not be empty")
            : Result<Glob, string>.Success(new Glob([pattern], dotted: false));
    }

    /// <summary>Whether the glob matches the name. The empty name is the global namespace and matches only <c>**</c>.</summary>
    /// <param name="name">The namespace, type name or assembly name.</param>
    /// <returns>True when the whole name matches.</returns>
    public bool IsMatch(ReadOnlySpan<char> name) =>
        _dotted
            ? MatchSegments(0, name, name.Length == 0 ? -1 : 0)
            : MatchWithin(_segments[0], name);

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
            if (!MatchWithin(pattern, name[position..end]))
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

    // Classic star matching: remember the last star and retry from one character further on a mismatch.
    private static bool MatchWithin(string pattern, ReadOnlySpan<char> text)
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
            else if (p < pattern.Length && pattern[p] == text[t])
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
