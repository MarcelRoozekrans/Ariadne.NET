using System.Globalization;
using System.Text.Json;
using Ariadne.NET.Sync;

namespace Ariadne.NET.Hooks;

/// <summary>Reads the import cache without touching the network.</summary>
public static class CacheFreshness
{
    /// <summary>One warning per import whose cache is missing or no longer satisfies its version range.</summary>
    public static IReadOnlyList<string> Check(string root, IImportsLoader loader) => Read(root, loader).Warnings;

    /// <summary>The <c>name@resolvedTag</c> of every cached import.</summary>
    public static IReadOnlyList<string> Versions(string root, IImportsLoader loader) => Read(root, loader).Versions;

    /// <summary>Reads <c>imports.yaml</c> once and returns both the warnings and the cached versions.</summary>
    public static CacheStatus Read(string root, IImportsLoader loader)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(loader);

        var manifestPath = Path.Combine(root, ".architecture", "imports.yaml");
        if (!File.Exists(manifestPath))
        {
            return new CacheStatus([], []);
        }

        var loaded = loader.LoadFromFile(manifestPath);
        if (loaded.IsFailure)
        {
            return new CacheStatus([$"imports.yaml could not be read: {TextLimit.Shorten(loaded.Error.Message)}"], []);
        }

        var cacheRoot = Path.Combine(root, ".architecture", ".cache", "git");
        var warnings = new List<string>();
        var versions = new List<string>();
        foreach (var import in loaded.Value.Imports)
        {
            var name = TextLimit.Shorten(import.Name, 100);
            if (!ImportNameRule.IsSingleDirectoryName(import.Name, cacheRoot))
            {
                warnings.Add($"Import name '{name}' is not a plain directory name, so its cache is not read.");
                continue;
            }

            var range = SemVerRange.Parse(import.Version);
            if (range.IsFailure)
            {
                warnings.Add($"Import '{name}' has a version range '{TextLimit.Shorten(import.Version, 100)}' that cannot be read.");
                continue;
            }

            var tag = ReadResolvedTag(cacheRoot, import.Name);
            if (tag is not null)
            {
                versions.Add(string.Create(CultureInfo.InvariantCulture, $"{name}@{TextLimit.Shorten(tag, 100)}"));
            }

            if (tag is null || !SemVer.TryParse(tag, out var cached) || !range.Value.IsSatisfiedBy(cached!))
            {
                warnings.Add($"Import '{name}' is not cached for '{TextLimit.Shorten(import.Version, 100)}'; run 'ariadne sync'.");
            }
        }

        return new CacheStatus(warnings, versions);
    }

    private static string? ReadResolvedTag(string cacheRoot, string name)
    {
        var file = Path.Combine(cacheRoot, name, ".git-info.json");
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("resolvedTag", out var tag)
                && tag.ValueKind == JsonValueKind.String
                    ? tag.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
