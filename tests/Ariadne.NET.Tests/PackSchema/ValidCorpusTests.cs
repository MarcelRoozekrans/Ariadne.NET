using FluentAssertions;

namespace Ariadne.NET.Tests.PackSchema;

public class ValidCorpusTests
{
    public static TheoryData<string> ShippedPacks()
    {
        var root = PackSchemaHarness.RepoRoot();
        var files = Directory.GetFiles(Path.Combine(root, "src", "Ariadne.NET", "BuiltinPacks"), "*.yaml")
            .Append(Path.Combine(root, ".architecture", "rules.yaml"))
            .Concat(Directory.GetFiles(Path.Combine(root, "tests", "fixtures"), "*.yaml", SearchOption.AllDirectories))
            .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
        var data = new TheoryData<string>();
        foreach (var file in files)
        {
            data.Add(Path.GetRelativePath(root, file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ShippedPacks))]
    public void Every_shipped_pack_validates_and_loads(string relativePath)
    {
        var yaml = File.ReadAllText(Path.Combine(PackSchemaHarness.RepoRoot(), relativePath));

        PackSchemaHarness.LoaderAccepts(yaml).Should().BeTrue($"{relativePath} is a shipped pack");
        PackSchemaHarness.SchemaErrors(yaml).Should().BeEmpty($"{relativePath} must validate against the schema");
    }

    public static TheoryData<string> ValidPacks()
    {
        var root = PackSchemaHarness.RepoRoot();
        var dir = Path.Combine(root, "tests", "Ariadne.NET.Tests", "PackSchema", "ValidPacks");
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(dir, "*.yaml").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(root, file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ValidPacks))]
    public void Valid_pack_is_accepted_by_schema_and_loader(string relativePath)
    {
        var yaml = File.ReadAllText(Path.Combine(PackSchemaHarness.RepoRoot(), relativePath));

        PackSchemaHarness.LoaderAccepts(yaml).Should().BeTrue($"the loader must accept {relativePath}");
        PackSchemaHarness.SchemaErrors(yaml).Should().BeEmpty($"the schema must accept {relativePath} too");
    }
}
