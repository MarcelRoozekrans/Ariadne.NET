using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;

namespace Ariadne.NET.Hooks;

/// <summary>
/// Decides whether a C# file that is about to be written lands in a namespace and assembly no layer selects. The root is
/// the hook's working directory. Projects that no layer selects, such as tests, samples and tools, ask on every new file.
/// A non-literal <c>AssemblyName</c>, such as <c>$(Prefix).Core</c>, falls back to the project file name and can give a
/// false question. The namespace is found with a regular expression, so a <c>namespace X;</c> line inside a block comment
/// or a raw string can fool it. That is a known limit.
/// </summary>
public static partial class NewFilePlacement
{
    /// <summary>
    /// The question to put to the user, or null to let the write go ahead. A file that already exists, is not C#, sits
    /// under <c>obj</c> or <c>bin</c>, belongs to no project below <paramref name="root"/> or is covered by a pack without
    /// layer selectors is never questioned.
    /// </summary>
    public static string? Check(RulePack pack, string root, string filePath, string? content) =>
        Check(pack, root, filePath, content, treatAsNew: false);

    internal static string? Check(RulePack pack, string root, string filePath, string? content, bool treatAsNew)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(filePath);

        var layers = pack.Layers.Where(l => l.Namespaces.Count > 0 || l.Assemblies.Count > 0).Select(l => l.Name).ToArray();
        if (layers.Length == 0 || !filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalizedRoot = Normalize(root).TrimEnd('/');
        var normalized = Normalize(filePath);
        var relative = RelativeTo(normalizedRoot, normalized);
        if (relative is null)
        {
            return null;
        }

        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => string.Equals(s, "..", StringComparison.Ordinal)))
        {
            return null;
        }

        if (segments.Any(s => string.Equals(s, "obj", StringComparison.Ordinal) || string.Equals(s, "bin", StringComparison.Ordinal))
            || (!treatAsNew && File.Exists(Path.Combine(normalizedRoot, relative))))
        {
            return null;
        }

        var project = FindProject(normalizedRoot, segments);
        if (project is null)
        {
            return null;
        }

        var (assembly, rootNamespace) = ReadProject(project.Value.Csproj);
        var folderNamespace = string.Join('.', [rootNamespace, .. segments[project.Value.FolderDepth..^1].Select(Identifier)]);
        var ns = content is null ? null : DeclaredNamespace(content);
        ns ??= folderNamespace;
        if (LayerProbe.LayersOf(pack, assembly, ns).Count > 0)
        {
            return null;
        }

        return $"New file '{relative}' would be namespace '{ns}' in assembly '{assembly}', which no layer of the rules selects. "
            + $"Layers: {string.Join(", ", layers)}. Ask the user where it belongs or add a layer.";
    }

    // MSBuild turns a folder name into a namespace segment: invalid identifier characters become '_' and a leading digit gets '_'.
    private static string Identifier(string folder)
    {
        var chars = folder.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray();
        var text = new string(chars);
        return text.Length > 0 && char.IsDigit(text[0]) ? "_" + text : text;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string? RelativeTo(string root, string path)
    {
        if (path.StartsWith(root + "/", StringComparison.Ordinal))
        {
            return path[(root.Length + 1)..];
        }

        // A path that is not rooted is relative to the project root.
        return path.StartsWith('/') || (path.Length > 1 && path[1] == ':') ? null : path;
    }

    // The folder depth is the number of segments that lead to the project folder below the root.
    private static (string Csproj, int FolderDepth)? FindProject(string root, string[] segments)
    {
        for (var depth = segments.Length - 1; depth >= 0; depth--)
        {
            var folder = depth == 0 ? root : Path.Combine(root, string.Join('/', segments[..depth]));
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var projects = Directory.GetFiles(folder, "*.csproj");
            if (projects.Length > 0)
            {
                Array.Sort(projects, StringComparer.Ordinal);
                return (projects[0], depth);
            }
        }

        return null;
    }

    private static (string Assembly, string RootNamespace) ReadProject(string csproj)
    {
        var name = Path.GetFileNameWithoutExtension(csproj);
        string? assembly = null;
        string? rootNamespace = null;
        try
        {
            var doc = XDocument.Load(csproj);
            assembly = Literal(doc, "AssemblyName");
            rootNamespace = Literal(doc, "RootNamespace");
        }
        catch (Exception e) when (e is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            // An unreadable project falls back to its file name.
        }

        assembly ??= name;
        return (assembly, rootNamespace ?? assembly);
    }

    private static string? Literal(XDocument doc, string element) =>
        doc.Descendants().Where(e => string.Equals(e.Name.LocalName, element, StringComparison.Ordinal) && !IsConditional(e))
            .Select(e => e.Value.Trim())
            .FirstOrDefault(v => v.Length > 0 && !v.Contains("$(", StringComparison.Ordinal));

    private static bool IsConditional(XElement element) =>
        element.Attribute("Condition") is not null || element.Parent?.Attribute("Condition") is not null;

    // The first declaration wins.
    private static string? DeclaredNamespace(string content)
    {
        try
        {
            var match = NamespaceDeclaration().Match(content);
            return match.Success ? match.Groups["name"].Value : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^\s*namespace\s+(?<name>[\w.]+)\s*[;{]", RegexOptions.Multiline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NamespaceDeclaration();
}
