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

    public static bool TryParse(string input, out SemVer? version)
    {
        version = null;

        var match = VersionRegex().Match(input);
        if (!match.Success)
        {
            return false;
        }

        var major = int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture);
        var minor = int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture);
        var patch = match.Groups["patch"].Success
            ? int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture)
            : 0;

        version = new SemVer(major, minor, patch);
        return true;
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

    [GeneratedRegex(@"^v?(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionRegex();
}
