namespace Ariadne.NET.Sync;

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
