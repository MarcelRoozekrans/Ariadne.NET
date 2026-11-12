using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Tests.PackSchema;

/// <summary>
/// Loads every fenced <c>yaml</c> or <c>yml</c> block of the migration guide as a pack, so a block that stops loading,
/// validating or resolving fails CI.
/// </summary>
public sealed class MigrationGuideSnippetTests
{
    /// <summary>The migration guide page in this repository.</summary>
    internal static string PagePath { get; } =
        Path.Combine(PackSchemaHarness.RepoRoot(), "docs-site", "docs", "migrating-from-archunitnet.md");

    /// <summary>
    /// The body of every fenced block in <paramref name="markdown"/> whose info string's first word is <c>yaml</c> or
    /// <c>yml</c>, in page order, without the fence lines. A block inside a longer fence is part of that fence, not a block.
    /// As in CommonMark, a fence indented inside a list item loses that indentation from each body line.
    /// </summary>
    internal static IReadOnlyList<string> YamlBlocks(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var blocks = new List<string>();
        var fence = 0;
        var indent = 0;
        var isYaml = false;
        List<string>? body = null;
        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.TrimStart();
            var ticks = trimmed.Length - trimmed.TrimStart('`').Length;
            if (fence == 0)
            {
                if (ticks >= 3)
                {
                    fence = ticks;
                    indent = line.Length - trimmed.Length;
                    var info = trimmed[ticks..].Trim();
                    var word = info.Split(' ', 2)[0];
                    isYaml = string.Equals(word, "yaml", StringComparison.Ordinal) || string.Equals(word, "yml", StringComparison.Ordinal);
                    body = [];
                }

                continue;
            }

            if (ticks >= fence && trimmed[ticks..].Trim().Length == 0)
            {
                if (isYaml)
                {
                    blocks.Add(string.Join('\n', body!));
                }

                fence = 0;
                body = null;
                continue;
            }

            var strip = Math.Min(indent, line.Length - trimmed.Length);
            body!.Add(line[strip..]);
        }

        return blocks;
    }

    public static TheoryData<int> BlockIndexes()
    {
        var data = new TheoryData<int>();
        var count = File.Exists(PagePath) ? YamlBlocks(File.ReadAllText(PagePath)).Count : 0;
        for (var i = 0; i < count; i++)
        {
            data.Add(i);
        }

        return data;
    }

    [Fact]
    public void Extracts_yaml_and_yml_fences_with_titles()
    {
        const string Sample = """
            # Sample

            ```yaml
            schema: 2
            name: first
            ```

            ```yml title="x"
            name: second
            ```

            ```csharp
            var x = 1;
            ```

            ````md
            ```yaml
            name: nested
            ```
            ````
            """;

        YamlBlocks(Sample).Should().Equal("schema: 2\nname: first", "name: second");
    }

    [Fact]
    public void Page_has_yaml_blocks()
    {
        File.Exists(PagePath).Should().BeTrue($"the migration guide lives at {PagePath}");
        YamlBlocks(File.ReadAllText(PagePath)).Should().NotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(BlockIndexes))]
    public void Every_yaml_block_is_a_pack_that_loads_and_validates(int index)
    {
        var block = YamlBlocks(File.ReadAllText(PagePath))[index];

        block.Should().Contain("schema: 2", $"yaml block {index} must be a complete schema 2 pack");
        PackSchemaHarness.LoaderAccepts(block).Should().BeTrue($"the loader must accept yaml block {index}");
        PackSchemaHarness.SchemaErrors(block).Should().BeEmpty($"yaml block {index} must validate against the schema");

        var pack = new YamlRulePackLoader().LoadFromString(block).Value;
        if (pack.Extends.Count == 0)
        {
            PackSchemaHarness.ReferencesResolve(block).Should().BeTrue($"the references of yaml block {index} must resolve");
            return;
        }

        pack.Extends.Should().OnlyContain(
            static e => e.StartsWith("builtin://", StringComparison.Ordinal),
            $"yaml block {index} may extend only builtin:// packs, since import:// needs a working directory");

        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IRulePackResolver>().Resolve(pack);
        resolved.IsSuccess.Should().BeTrue(
            $"yaml block {index} must resolve with its parents: {(resolved.IsSuccess ? string.Empty : resolved.Error.Message + " " + resolved.Error.Detail)}");
    }
}
