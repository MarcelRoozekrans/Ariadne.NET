using System.Runtime.InteropServices;
using System.Text.Json;
using Ariadne.NET.Analysis;

namespace Ariadne.NET.Workspaces;

/// <summary>
/// Reads the packages a project resolves from its <c>obj/project.assets.json</c>: the <c>type: package</c> entries of the
/// <c>targets</c> of one target framework, with the shortest dependency path to each from a direct reference or a referenced project.
/// </summary>
internal static class ResolvedPackageReader
{
    /// <summary>
    /// Every package entry of the target framework counts, whatever its assets, so analyzer and build-only packages are listed.
    /// <c>type: project</c> entries are not packages, but they carry the packages of a referenced project, so such a package counts
    /// for the referencing project and its path names the project. A referenced project's <c>PrivateAssets</c> analyzer packages are
    /// absent from the referencing project's <c>targets</c>, so they are not listed there; they stay enforced on the referenced
    /// project. <c>IsDirect</c> comes from the project's own <c>projectFileDependencyGroups</c> entry for the framework, since
    /// <c>targets</c> lists direct and transitive packages alike. The path is found breadth first over the <c>dependencies</c> of
    /// <c>targets</c>, from the direct references and referenced projects in the order the file lists them.
    /// The packages are unknown, not empty, when the file has no <c>targets</c> entry or no dependency group for the framework, since
    /// an empty list would report a project clean that was never checked.
    /// </summary>
    /// <param name="assetsPath">The assets file.</param>
    /// <param name="targetFramework">The target framework of the project, or null when the file holds a single one.</param>
    /// <param name="packages">The packages, in the order of the file; empty when they are unknown.</param>
    /// <returns>True when the packages are known.</returns>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="JsonException">The file is not valid JSON.</exception>
    public static bool TryRead(string assetsPath, string? targetFramework, out IReadOnlyList<ResolvedPackage> packages)
    {
        ArgumentNullException.ThrowIfNull(assetsPath);

        packages = [];
        using var stream = File.OpenRead(assetsPath);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("targets", out var targets)
            || Select(targets, targetFramework) is not { } target
            || !root.TryGetProperty("projectFileDependencyGroups", out var groups)
            || Select(groups, targetFramework) is not { } group)
        {
            return false;
        }

        packages = Resolve(Nodes(target), Roots(group)).ToArray();
        return true;
    }

    // A node is a targets entry keyed "id/version", with the ids it depends on.
    private sealed record Node(string Id, string Version, bool IsPackage, List<string> Dependencies);

    private static List<Node> Nodes(JsonElement target)
    {
        var nodes = new List<Node>();
        foreach (var entry in target.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var key = entry.Name;
            var slash = key.IndexOf('/', StringComparison.Ordinal);
            var id = slash < 0 ? key : key[..slash];
            var version = slash < 0 ? "" : key[(slash + 1)..];
            var isPackage = entry.Value.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && string.Equals(type.GetString(), "package", StringComparison.Ordinal);
            nodes.Add(new Node(id, version, isPackage, Dependencies(entry.Value)));
        }

        return nodes;
    }

    private static List<string> Dependencies(JsonElement entry)
    {
        var dependencies = new List<string>();
        if (entry.TryGetProperty("dependencies", out var declared) && declared.ValueKind == JsonValueKind.Object)
        {
            foreach (var dependency in declared.EnumerateObject())
            {
                dependencies.Add(dependency.Name);
            }
        }

        return dependencies;
    }

    // "Id >= 1.0.0" or "Id [1.0.0, )": the id is the text before the first space.
    private static List<string> Roots(JsonElement group)
    {
        var roots = new List<string>();
        if (group.ValueKind != JsonValueKind.Array)
        {
            return roots;
        }

        foreach (var item in group.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text)
            {
                var space = text.IndexOf(' ', StringComparison.Ordinal);
                roots.Add(space < 0 ? text : text[..space]);
            }
        }

        return roots;
    }

    private static List<ResolvedPackage> Resolve(List<Node> nodes, List<string> roots)
    {
        var byId = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        foreach (ref readonly var node in CollectionsMarshal.AsSpan(nodes))
        {
            byId.TryAdd(node.Id, node);
        }

        var direct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ref readonly var root in CollectionsMarshal.AsSpan(roots))
        {
            if (byId.TryGetValue(root, out var node) && node.IsPackage)
            {
                direct.Add(node.Id);
            }
        }

        var paths = ShortestPaths(byId, roots);
        var packages = new List<ResolvedPackage>();
        foreach (ref readonly var node in CollectionsMarshal.AsSpan(nodes))
        {
            if (node.IsPackage)
            {
                // A package no direct reference reaches should not occur; list it anyway, since it is in the resolved graph.
                packages.Add(new ResolvedPackage(node.Id, node.Version, direct.Contains(node.Id), paths.GetValueOrDefault(node.Id) ?? [node.Id]));
            }
        }

        return packages;
    }

    private static Dictionary<string, string[]> ShortestPaths(Dictionary<string, Node> byId, List<string> roots)
    {
        var paths = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<Node>();
        foreach (ref readonly var root in CollectionsMarshal.AsSpan(roots))
        {
            if (byId.TryGetValue(root, out var node) && paths.TryAdd(node.Id, [node.Id]))
            {
                queue.Enqueue(node);
            }
        }

        while (queue.TryDequeue(out var current))
        {
            var parentPath = paths[current.Id];
            foreach (ref readonly var dependency in CollectionsMarshal.AsSpan(current.Dependencies))
            {
                if (byId.TryGetValue(dependency, out var next) && !paths.ContainsKey(next.Id))
                {
                    paths[next.Id] = [.. parentPath, next.Id];
                    queue.Enqueue(next);
                }
            }
        }

        return paths;
    }

    // net10.0-windows7.0 extends net10.0 and net10.0-windows: after the framework comes "-" and a platform, or, when the framework
    // already names a platform, its version. A bare digit after a plain framework is another framework, so net4 never matches net48.
    private static bool ExtendsWithPlatform(string name, string targetFramework) =>
        name.Length > targetFramework.Length
        && name.StartsWith(targetFramework, StringComparison.OrdinalIgnoreCase)
        && (name[targetFramework.Length] == '-'
            || (char.IsAsciiDigit(name[targetFramework.Length]) && targetFramework.Contains('-', StringComparison.Ordinal)));

    // The framework's entry: by full name, then by the name before a runtime identifier suffix (net10.0/linux-x64), then a name that
    // extends the framework with a platform (net10.0-windows7.0). Without a framework, the file's only entry. Null when none fits, so
    // a file restored for another framework is unknown and not taken for this one.
    private static JsonElement? Select(JsonElement entries, string? targetFramework)
    {
        if (entries.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonProperty? only = null;
        var count = 0;
        JsonProperty? byBase = null;
        JsonProperty? byPlatform = null;
        foreach (var entry in entries.EnumerateObject())
        {
            count++;
            only ??= entry;
            if (targetFramework is null)
            {
                continue;
            }

            if (string.Equals(entry.Name, targetFramework, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }

            var slash = entry.Name.IndexOf('/', StringComparison.Ordinal);
            var name = slash < 0 ? entry.Name : entry.Name[..slash];
            if (string.Equals(name, targetFramework, StringComparison.OrdinalIgnoreCase))
            {
                byBase ??= entry;
            }
            else if (ExtendsWithPlatform(name, targetFramework))
            {
                byPlatform ??= entry;
            }
        }

        if (targetFramework is null)
        {
            return count == 1 ? only?.Value : null;
        }

        return (byBase ?? byPlatform)?.Value;
    }
}
