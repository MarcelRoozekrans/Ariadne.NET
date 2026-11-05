using System.Globalization;
using System.Text.RegularExpressions;

namespace Ariadne.NET.Sync;

public sealed partial class SemVer : IComparable<SemVer>, IEquatable<SemVer>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    public SemVer(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>
    /// Parses <c>[v]major.minor[.patch]</c> with ASCII digits. Null, any other text and a part above
    /// <see cref="int.MaxValue"/> return <see langword="false"/>; nothing throws.
    /// </summary>
    public static bool TryParse(string? input, out SemVer? version)
    {
        version = null;
        if (input is null)
        {
            return false;
        }

        var match = VersionRegex().Match(input);
        if (!match.Success
            || !TryParsePart(match.Groups["major"], out var major)
            || !TryParsePart(match.Groups["minor"], out var minor)
            || !TryParsePart(match.Groups["patch"], out var patch))
        {
            return false;
        }

        version = new SemVer(major, minor, patch);
        return true;
    }

    /// <summary>An absent group is 0, as for a missing patch; a value that does not fit an int fails.</summary>
    private static bool TryParsePart(Group group, out int value)
    {
        if (!group.Success)
        {
            value = 0;
            return true;
        }

        return int.TryParse(group.ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    public int CompareTo(SemVer? other)
    {
        if (other is null)
        {
            return 1;
        }

        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0)
        {
            return majorComparison;
        }

        var minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0)
        {
            return minorComparison;
        }

        return Patch.CompareTo(other.Patch);
    }

    public bool Equals(SemVer? other) => other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) => Equals(obj as SemVer);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);

    public static bool operator ==(SemVer? left, SemVer? right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(SemVer? left, SemVer? right) => !(left == right);

    public static bool operator <(SemVer? left, SemVer? right) => left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(SemVer? left, SemVer? right) => left is null || left.CompareTo(right) <= 0;

    public static bool operator >(SemVer? left, SemVer? right) => left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(SemVer? left, SemVer? right) => left is null ? right is null : left.CompareTo(right) >= 0;

    [GeneratedRegex(@"^v?(?<major>[0-9]+)\.(?<minor>[0-9]+)(?:\.(?<patch>[0-9]+))?$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionRegex();
}
