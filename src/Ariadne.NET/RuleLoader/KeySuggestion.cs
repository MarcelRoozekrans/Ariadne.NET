namespace Ariadne.NET.RuleLoader;

/// <summary>Suggests the allowed key a misspelled key was probably meant to be.</summary>
internal static class KeySuggestion
{
    private const int MaxDistance = 2;

    /// <summary>
    /// The allowed key at the lowest Levenshtein distance from <paramref name="key"/>, if that distance is at most 2.
    /// Ties go to the key listed first. Ordinal and case-sensitive.
    /// </summary>
    /// <param name="key">The unknown key.</param>
    /// <param name="allowed">The keys allowed where the unknown key was found.</param>
    /// <returns>The suggestion, or null when no allowed key is close enough.</returns>
    public static string? Closest(string key, IReadOnlyList<string> allowed)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(allowed);

        string? best = null;
        var bestDistance = MaxDistance + 1;
        for (var i = 0; i < allowed.Count; i++)
        {
            var distance = Distance(key, allowed[i]);
            if (distance < bestDistance)
            {
                best = allowed[i];
                bestDistance = distance;
            }
        }

        return best;
    }

    // Two-row Levenshtein distance.
    private static int Distance(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > MaxDistance)
        {
            return int.MaxValue;
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(substitution, Math.Min(previous[j], current[j - 1]) + 1);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
