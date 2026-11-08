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
    /// for the referencing project and its path names the project. <c>IsDirect</c> comes from the project's own
    /// <c>projectFileDependencyGroups</c> entry for the framework, since <c>targets</c> lists direct and transitive packages alike.
    /// The path is found breadth first over the <c>dependencies</c> of <c>targets</c>, from the direct references and referenced
    /// projects in the order the file lists them.
    /// </summary>
    /// <param name="assetsPath">The assets file.</param>
    /// <param name="targetFramework">The target framework of the project, or null to use the single one in the file.</param>
    /// <returns>The packages, in the order of the file.</returns>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="JsonException">The file is not valid JSON.</exception>
    public static IReadOnlyList<ResolvedPackage> Read(string assetsPath, string? targetFramework)
    {
        ArgumentNullException.ThrowIfNull(assetsPath);

        using var stream = File.OpenRead(assetsPath);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (!root.TryGetProperty("targets", out var targets) || Select(targets, targetFramework) is not { } target)
        {
            return [];
        }

        var nodes = Nodes(target);
        var roots = root.TryGetProperty("projectFileDependencyGroups", out var groups) && Select(groups, targetFramework) is { } group
            ? Roots(group)
            : [];
        return Resolve(nodes, roots);
    }

    // A node is a targets entry keyed "id/version", with the ids it depends on.
    private sealed record Node(string Id, string Version, bool IsPackage, List<string> Dependencies);

    private static List<Node> Nodes(JsonElement target)
    {
        var nodes = new List<Node>();
        foreach (var entry in target.EnumerateObject())
        {
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

    // The framework's entry: by name, then the name before a runtime identifier suffix, then a name that extends the framework with
    // a platform such as net10.0-windows7.0. Without a framework, or when the file has a single entry, that entry.
    private static JsonElement? Select(JsonElement entries, string? targetFramework)
    {
        if (entries.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonProperty? only = null;
        var count = 0;
        JsonProperty? byPrefix = null;
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

            if (entry.Name.StartsWith(targetFramework + "-", StringComparison.OrdinalIgnoreCase))
            {
                byPrefix ??= entry;
            }
        }

        if (byPrefix is { } prefixed)
        {
            return prefixed.Value;
        }

        return count == 1 || targetFramework is null ? only?.Value : null;
    }
}
