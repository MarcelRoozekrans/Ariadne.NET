namespace Ariadne.NET.Hooks.Tests;

/// <summary>A unique temp project folder with an .architecture folder, deleted on dispose.</summary>
internal sealed class TempProject : IDisposable
{
    public TempProject()
    {
        Root = Path.Combine(Path.GetTempPath(), $"ariadne-hooks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(Root, ".architecture"));
    }

    public string Root { get; }

    public void WriteRules(string yaml = "schema: 2\nname: shop\nextends:\n  - builtin://clean-architecture\n") =>
        File.WriteAllText(Path.Combine(Root, ".architecture", "rules.yaml"), yaml);

    public void WriteImports(string name, string version) =>
        File.WriteAllText(
            Path.Combine(Root, ".architecture", "imports.yaml"),
            $"imports:\n  - name: {name}\n    type: git\n    url: https://example.com/{name}.git\n    version: \"{version}\"\n");

    public void WriteCache(string name, string tag)
    {
        var dir = Path.Combine(Root, ".architecture", ".cache", "git", name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, ".git-info.json"),
            $$"""{"url":"https://example.com/{{name}}.git","resolvedTag":"{{tag}}","commitSha":"abc","syncedAt":"2026-01-01T00:00:00+00:00"}""");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temp folder must not fail a test.
        }
    }
}
