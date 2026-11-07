using System.Globalization;
using System.Text.Json;
using Ariadne.NET.Sync;

namespace Ariadne.NET.Hooks;

/// <summary>Reads the import cache without touching the network.</summary>
public static class CacheFreshness
{
    /// <summary>One warning per import whose cache is missing or no longer satisfies its version range.</summary>
    public static IReadOnlyList<string> Check(string root, IImportsLoader loader)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(loader);

        var manifest = LoadManifest(root, loader, out var failure);
        if (failure is not null)
        {
            return [failure];
        }

        var warnings = new List<string>();
        foreach (var import in manifest)
        {
            var tag = ReadResolvedTag(root, import.Name);
            var range = SemVerRange.Parse(import.Version);
            if (tag is null || range.IsFailure || !SemVer.TryParse(tag, out var cached) || !range.Value.IsSatisfiedBy(cached!))
            {
                warnings.Add($"Import '{import.Name}' is not cached for '{import.Version}'; run 'ariadne sync'.");
            }
        }

        return warnings;
    }

    /// <summary>The <c>name@resolvedTag</c> of every cached import.</summary>
    public static IReadOnlyList<string> Versions(string root, IImportsLoader loader)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(loader);

        var manifest = LoadManifest(root, loader, out _);
        var versions = new List<string>();
        foreach (var import in manifest)
        {
            var tag = ReadResolvedTag(root, import.Name);
            if (tag is not null)
            {
                versions.Add(string.Create(CultureInfo.InvariantCulture, $"{import.Name}@{tag}"));
            }
        }

        return versions;
    }

    private static IList<ImportDefinition> LoadManifest(string root, IImportsLoader loader, out string? failure)
    {
        failure = null;
        var path = Path.Combine(root, ".architecture", "imports.yaml");
        if (!File.Exists(path))
        {
            return [];
        }

        var loaded = loader.LoadFromFile(path);
        if (loaded.IsFailure)
        {
            failure = $"imports.yaml could not be read: {loaded.Error.Message}";
            return [];
        }

        return loaded.Value.Imports;
    }

    private static string? ReadResolvedTag(string root, string name)
    {
        var file = Path.Combine(root, ".architecture", ".cache", "git", name, ".git-info.json");
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
