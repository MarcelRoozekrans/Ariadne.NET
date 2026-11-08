using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Analysis;

/// <summary>
/// The candidate filter of member uses: the member segment, the text after the last dot, of every <c>members</c> pattern of a
/// pack. The walker records a use only when its simple name could match one of them, so only such a use pays for an edge and,
/// later, for its override chain. An override or an implicit interface implementation keeps the name of the member it
/// overrides or implements, so the filter loses no match. Built once per run, matched without allocating.
/// </summary>
internal sealed class MemberNameFilter
{
    private const string AnySegment = "*";
    private const string AnySegments = "**";

    private readonly Glob[] _segments;
    private readonly bool _matchesAll;

    private MemberNameFilter(Glob[] segments, bool matchesAll)
    {
        _segments = segments;
        _matchesAll = matchesAll;
    }

    /// <summary>The filter of a pack without <c>members</c> patterns: it lets nothing through, so no member edge is recorded.</summary>
    public static MemberNameFilter Empty { get; } = new([], matchesAll: false);

    /// <summary>True when no name can pass, so the walker skips member uses without looking at them.</summary>
    public bool IsEmpty => !_matchesAll && _segments.Length == 0;

    /// <summary>
    /// Collects the member segment of every <c>members</c> pattern on the <c>to</c> selectors of the pack's rules. A segment of
    /// <c>*</c> or <c>**</c> lets every name through.
    /// </summary>
    /// <param name="pack">The resolved pack.</param>
    /// <returns>The filter, or <see cref="Empty"/> when the pack has no <c>members</c> pattern.</returns>
    public static MemberNameFilter From(RulePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        var segments = new List<string>();
        foreach (var rule in pack.Rules)
        {
            foreach (var pattern in rule.To.Members)
            {
                var segment = pattern[(pattern.LastIndexOf('.') + 1)..];
                if (string.Equals(segment, AnySegment, StringComparison.Ordinal) || string.Equals(segment, AnySegments, StringComparison.Ordinal))
                {
                    return new MemberNameFilter([], matchesAll: true);
                }

                if (!segments.Contains(segment, StringComparer.Ordinal))
                {
                    segments.Add(segment);
                }
            }
        }

        return segments.Count == 0 ? Empty : new MemberNameFilter(Glob.CompileAll(segments, Glob.Dotted), matchesAll: false);
    }

    /// <summary>Whether a member with this simple name could match a <c>members</c> pattern of the pack.</summary>
    /// <param name="memberName">The member's simple name, <c>new</c> for a constructor.</param>
    /// <returns>True when it could.</returns>
    public bool CouldMatch(string memberName) => _matchesAll || Glob.AnyMatch(_segments, memberName);
}
