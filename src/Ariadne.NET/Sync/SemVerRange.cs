using System.Globalization;
using System.Text.RegularExpressions;

namespace Ariadne.NET.Sync;

public sealed partial class SemVer : IComparable<SemVer>
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

        var major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minor = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var patch = match.Groups[3].Success
            ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
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

    [GeneratedRegex(@"^v?(\d+)\.(\d+)(?:\.(\d+))?$")]
    private static partial Regex VersionRegex();
}

public sealed class SemVerRange
{
    private readonly Func<SemVer, bool> _predicate;

    private SemVerRange(Func<SemVer, bool> predicate)
    {
        _predicate = predicate;
    }

    public bool IsSatisfiedBy(SemVer version) => _predicate(version);

    public string? FindBestMatch(IEnumerable<string> tags)
    {
        string? bestTag = null;
        SemVer? bestVersion = null;

        foreach (var tag in tags)
        {
            if (!SemVer.TryParse(tag, out var version) || !IsSatisfiedBy(version!))
            {
                continue;
            }

            if (bestVersion is null || version!.CompareTo(bestVersion) > 0)
            {
                bestVersion = version;
                bestTag = tag;
            }
        }

        return bestTag;
    }

    public static SemVerRange Parse(string range)
    {
        if (string.IsNullOrEmpty(range))
        {
            throw new FormatException($"Invalid semver range: '{range}'");
        }

        if (range.StartsWith('^'))
        {
            var versionPart = range[1..];
            if (!SemVer.TryParse(versionPart, out var baseVersion))
            {
                throw new FormatException($"Invalid semver range: '{range}'");
            }

            return new SemVerRange(v =>
            {
                if (baseVersion!.Major == 0)
                {
                    // For ^0.x.y, only allow patch bumps within same major.minor
                    return v.Major == baseVersion.Major
                        && v.Minor == baseVersion.Minor
                        && v.Patch >= baseVersion.Patch;
                }

                // For ^X.Y.Z where X>0, allow minor+patch bumps within same major
                return v.Major == baseVersion.Major
                    && v.CompareTo(baseVersion) >= 0;
            });
        }

        if (range.StartsWith('~'))
        {
            var versionPart = range[1..];
            if (!SemVer.TryParse(versionPart, out var baseVersion))
            {
                throw new FormatException($"Invalid semver range: '{range}'");
            }

            return new SemVerRange(v =>
                v.Major == baseVersion!.Major
                && v.Minor == baseVersion.Minor
                && v.Patch >= baseVersion.Patch);
        }

        // Exact match
        if (!SemVer.TryParse(range, out var exactVersion))
        {
            throw new FormatException($"Invalid semver range: '{range}'");
        }

        return new SemVerRange(v => v.CompareTo(exactVersion!) == 0);
    }
}
