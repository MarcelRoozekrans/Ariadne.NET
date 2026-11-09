using FluentAssertions;

namespace Ariadne.NET.Tests.PackSchema;

public class InvalidCorpusTests
{
    public static TheoryData<string> InvalidPacks() => Files("InvalidPacks");

    public static TheoryData<string> KnownGaps() => Files("KnownGaps");

    public static TheoryData<string> ShapeGaps() => Files("ShapeGaps");

    private static TheoryData<string> Files(string folder)
    {
        var root = PackSchemaHarness.RepoRoot();
        var dir = Path.Combine(root, "tests", "Ariadne.NET.Tests", "PackSchema", folder);
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(dir, "*.yaml").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(root, file));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(InvalidPacks))]
    public void Invalid_pack_is_rejected_by_schema_and_loader(string relativePath)
    {
        var yaml = File.ReadAllText(Path.Combine(PackSchemaHarness.RepoRoot(), relativePath));

        PackSchemaHarness.LoaderAccepts(yaml).Should().BeFalse($"the loader must reject {relativePath}");
        PackSchemaHarness.SchemaErrors(yaml).Should().NotBeEmpty($"the schema must reject {relativePath} too");
    }

    [Theory]
    [MemberData(nameof(KnownGaps))]
    public void Known_gap_is_accepted_by_schema_and_rejected_by_reference_check(string relativePath)
    {
        var yaml = File.ReadAllText(Path.Combine(PackSchemaHarness.RepoRoot(), relativePath));

        PackSchemaHarness.LoaderAccepts(yaml).Should().BeTrue($"{relativePath} is well formed, so the loader reads it");
        PackSchemaHarness.ReferencesResolve(yaml).Should().BeFalse($"the reference check must reject {relativePath}");
        PackSchemaHarness.SchemaErrors(yaml).Should().BeEmpty($"{relativePath} is a cross-reference the schema cannot see");
    }

    [Theory]
    [MemberData(nameof(ShapeGaps))]
    public void Shape_gap_is_accepted_by_schema_and_rejected_by_loader(string relativePath)
    {
        var yaml = File.ReadAllText(Path.Combine(PackSchemaHarness.RepoRoot(), relativePath));

        PackSchemaHarness.LoaderAccepts(yaml).Should().BeFalse($"the loader must reject {relativePath}");
        PackSchemaHarness.SchemaErrors(yaml).Should().BeEmpty($"{relativePath} is a rule JSON Schema cannot express");
    }
}
