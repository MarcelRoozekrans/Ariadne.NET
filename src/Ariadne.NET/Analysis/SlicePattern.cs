using ZeroAlloc.Results;

namespace Ariadne.NET.Analysis;

/// <summary>
/// A dotted namespace pattern with exactly one capture that names a slice: <c>(*)</c> captures exactly one segment and
/// <c>(**)</c> one or more. The capture is a whole segment and never matches zero segments; the segments before and after
/// it form ordinary dotted globs. Built once, matched without allocating unless a slice is captured.
/// </summary>
internal sealed class SlicePattern
{
    private const string OneSegment = "(*)";
    private const string AnySegments = "(**)";

    private readonly Glob? _prefix;
    private readonly Glob? _suffix;
    private readonly bool _multiSegment;

    private SlicePattern(Glob? prefix, Glob? suffix, bool multiSegment)
    {
        _prefix = prefix;
        _suffix = suffix;
        _multiSegment = multiSegment;
    }

    /// <summary>Compiles a pattern.</summary>
    /// <param name="pattern">The pattern to compile.</param>
    /// <returns>The pattern, or the message <see cref="Validate"/> gives when it is malformed.</returns>
    public static Result<SlicePattern, string> Compile(string pattern)
    {
        var error = Validate(pattern);
        if (error is not null)
        {
            return Result<SlicePattern, string>.Failure(error);
        }

        var segments = pattern.Split('.');
        var capture = Array.FindIndex(segments, IsCapture);
        var prefix = capture == 0 ? null : Glob.Dotted(string.Join('.', segments, 0, capture)).Value;
        var suffix = capture == segments.Length - 1 ? null : Glob.Dotted(string.Join('.', segments, capture + 1, segments.Length - capture - 1)).Value;
        return Result<SlicePattern, string>.Success(
            new SlicePattern(prefix, suffix, string.Equals(segments[capture], AnySegments, StringComparison.Ordinal)));
    }

    /// <summary>Checks a pattern without compiling it.</summary>
    /// <param name="pattern">The pattern to check.</param>
    /// <returns><see langword="null"/> when the pattern is valid, else a message for the loader.</returns>
    public static string? Validate(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length == 0)
        {
            return "pattern must not be empty";
        }

        var captures = 0;
        foreach (var segment in pattern.Split('.'))
        {
            if (IsCapture(segment))
            {
                captures++;
                continue;
            }

            if (segment.Contains(OneSegment, StringComparison.Ordinal) || segment.Contains(AnySegments, StringComparison.Ordinal))
            {
                return $"capture must be a whole segment in '{pattern}'";
            }

            var error = Glob.SegmentError(pattern, segment);
            if (error is not null)
            {
                return error;
            }
        }

        return captures == 1 ? null : $"pattern '{pattern}' must have exactly one capture, (*) or (**)";
    }

    /// <summary>
    /// Captures the slice a namespace belongs to. The earliest capture start wins; for <c>(**)</c>, the shortest capture
    /// that lets the rest of the pattern match.
    /// </summary>
    /// <param name="ns">The namespace; the empty namespace is the global one and has no slice.</param>
    /// <param name="slice">The captured segments, dots included, or the empty string on a miss.</param>
    /// <returns>True when the pattern captures a slice.</returns>
    public bool TryCapture(ReadOnlySpan<char> ns, out string slice)
    {
        for (var start = 0; start < ns.Length; start = NextSegment(ns, start))
        {
            if (!PrefixMatches(ns, start))
            {
                continue;
            }

            var end = SegmentEnd(ns, start);
            while (true)
            {
                if (SuffixMatches(ns, end))
                {
                    slice = ns[start..end].ToString();
                    return true;
                }

                if (!_multiSegment || end == ns.Length)
                {
                    break;
                }

                end = SegmentEnd(ns, end + 1);
            }
        }

        slice = "";
        return false;
    }

    private static bool IsCapture(string segment) =>
        string.Equals(segment, OneSegment, StringComparison.Ordinal) || string.Equals(segment, AnySegments, StringComparison.Ordinal);

    // The prefix is everything before the dot ahead of start; at start 0 it is empty, which only a missing prefix or ** matches.
    private bool PrefixMatches(ReadOnlySpan<char> ns, int start) =>
        _prefix is null ? start == 0 : _prefix.IsMatch(start == 0 ? [] : ns[..(start - 1)]);

    // The suffix is everything after the dot behind end; at the end of the name it is empty, which only a missing suffix or ** matches.
    private bool SuffixMatches(ReadOnlySpan<char> ns, int end) =>
        _suffix is null ? end == ns.Length : _suffix.IsMatch(end == ns.Length ? [] : ns[(end + 1)..]);

    private static int SegmentEnd(ReadOnlySpan<char> ns, int position)
    {
        var end = position;
        while (end < ns.Length && ns[end] != '.')
        {
            end++;
        }

        return end;
    }

    private static int NextSegment(ReadOnlySpan<char> ns, int start)
    {
        var end = SegmentEnd(ns, start);
        return end == ns.Length ? ns.Length : end + 1;
    }
}
