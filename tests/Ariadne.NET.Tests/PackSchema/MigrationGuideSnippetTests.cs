using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.RepresentationModel;

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

        if (fence != 0)
        {
            throw new FormatException("The markdown has an unclosed code fence, so the blocks after it would be misread.");
        }

        return blocks;
    }

    /// <summary>The exact header of the Daedalus test map on the guide.</summary>
    private const string DaedalusMapHeader = "| Daedalus test | Pack rule | Notes |";

    /// <summary>
    /// The rows of the table in <paramref name="markdown"/> whose header is exactly <c>| Daedalus test | Pack rule | Notes |</c>,
    /// as the first two cells with backticks stripped and surrounding spaces trimmed.
    /// </summary>
    internal static IReadOnlyList<(string Test, string PackRule)> DaedalusMap(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var rows = new List<(string Test, string PackRule)>();
        var lines = markdown.Split('\n').Select(static l => l.TrimEnd('\r').Trim()).ToList();
        var header = lines.IndexOf(DaedalusMapHeader);
        if (header < 0)
        {
            return rows;
        }

        // Skip the header and the separator row; the table ends at the first line that is not a row.
        foreach (var line in lines.Skip(header + 2).TakeWhile(static l => l.StartsWith('|')))
        {
            var cells = line.Trim('|').Split('|');
            rows.Add((Strip(cells[0]), Strip(cells[1])));
        }

        return rows;
    }

    /// <summary>
    /// The rule or layer ids in a Pack rule cell: the cell without backticks, split on <c>, </c>. A cell that reads
    /// <c>stays in C#</c> or <c>not needed</c> names no id.
    /// </summary>
    internal static IReadOnlyList<string> PackRuleIds(string packRule)
    {
        ArgumentNullException.ThrowIfNull(packRule);

        var cell = Strip(packRule);
        if (string.Equals(cell, "stays in C#", StringComparison.Ordinal) || string.Equals(cell, "not needed", StringComparison.Ordinal))
        {
            return [];
        }

        return cell.Split(", ", StringSplitOptions.TrimEntries);
    }

    private static string Strip(string cell) => cell.Replace("`", string.Empty, StringComparison.Ordinal).Trim();

    /// <summary>The worked example's pack: the one yaml block whose <c>name</c> is <c>daedalus</c>, as a YAML mapping.</summary>
    private static List<YamlMappingNode> DaedalusPacks(string markdown) =>
        YamlBlocks(markdown)
            .Select(static block =>
            {
                var stream = new YamlStream();
                using var reader = new StringReader(block);
                stream.Load(reader);
                return (YamlMappingNode)stream.Documents[0].RootNode;
            })
            .Where(static root => root.Children.TryGetValue(new YamlScalarNode("name"), out var name)
                && string.Equals(((YamlScalarNode)name).Value, "daedalus", StringComparison.Ordinal))
            .ToList();

    private static IEnumerable<YamlMappingNode> Entries(YamlMappingNode pack, string key) =>
        pack.Children.TryGetValue(new YamlScalarNode(key), out var list)
            ? ((YamlSequenceNode)list).Children.Cast<YamlMappingNode>()
            : [];

    private static string? Scalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var value) ? ((YamlScalarNode)value).Value : null;

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
    public void Strips_the_indent_of_a_fence_inside_a_list_item()
    {
        const string Sample = "1. Step:\n\n   ```yaml\n   schema: 2\n   layers:\n     - id: a\n   ```\n\nAfter.";

        YamlBlocks(Sample).Should().Equal("schema: 2\nlayers:\n  - id: a");
    }

    [Fact]
    public void Throws_on_an_unclosed_fence()
    {
        const string Sample = "```yaml\nschema: 2\nname: open\n";

        var act = () => YamlBlocks(Sample);

        act.Should().Throw<FormatException>().WithMessage("*unclosed*");
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

    [Fact]
    public void DaedalusMap_reads_the_map_table()
    {
        const string Sample = """
            Intro text.

            | Daedalus test | Pack rule | Notes |
            |---|---|---|
            | `First_fact` | `DAE-X` | A rule. |
            | `Second_fact` | stays in C# | A reason. |
            | `Third_fact` | `DAE-X`, `DAE-Y` | Two rules. |

            | Other | Table | Here |
            |---|---|---|
            | `Not_a_row` | `DAE-Z` | Ignored. |
            """;

        var map = DaedalusMap(Sample);

        map.Should().Equal(("First_fact", "DAE-X"), ("Second_fact", "stays in C#"), ("Third_fact", "DAE-X, DAE-Y"));
        PackRuleIds(map[2].PackRule).Should().Equal("DAE-X", "DAE-Y");
        PackRuleIds("`DAE-X`, `DAE-Y`").Should().Equal("DAE-X", "DAE-Y");
        PackRuleIds("stays in C#").Should().BeEmpty();
        PackRuleIds("not needed").Should().BeEmpty();
    }

    [Fact]
    public void Daedalus_map_names_only_rules_of_the_pack()
    {
        var markdown = File.ReadAllText(PagePath);
        var packs = DaedalusPacks(markdown);
        packs.Should().ContainSingle("the worked example is one yaml block named daedalus");
        var pack = packs[0];

        var ruleIds = Entries(pack, "rules").Select(static r => Scalar(r, "id")!).ToList();
        var knownIds = ruleIds.Concat(Entries(pack, "layers").Select(static l => Scalar(l, "id")!)).ToHashSet(StringComparer.Ordinal);

        var map = DaedalusMap(markdown);
        map.Should().HaveCount(37, "the map has one row per Daedalus fact");
        map.Select(static row => row.Test).Should().OnlyHaveUniqueItems("each Daedalus test is mapped once");

        var mapped = map.SelectMany(static row => PackRuleIds(row.PackRule)).ToHashSet(StringComparer.Ordinal);
        mapped.Should().BeSubsetOf(knownIds, "the map names only rule and layer ids of the daedalus pack");
        ruleIds.Should().BeSubsetOf(mapped, "every rule of the daedalus pack answers a Daedalus fact");
    }

    [Fact]
    public void Load_bearing_facts_map_to_rules_that_require_their_target()
    {
        var markdown = File.ReadAllText(PagePath);
        var packs = DaedalusPacks(markdown);
        packs.Should().ContainSingle();
        var rules = Entries(packs[0], "rules").ToDictionary(static r => Scalar(r, "id")!, StringComparer.Ordinal);
        var requiringTarget = rules
            .Where(static r => string.Equals(Scalar(r.Value, "requireTarget"), "true", StringComparison.Ordinal))
            .Select(static r => r.Key)
            .ToHashSet(StringComparer.Ordinal);

        var map = DaedalusMap(markdown);
        string[] markers = ["IsLoaded", "AreLoaded", "RulesAreLoadBearing", "RuleIsLoadBearing", "TheNoCopyRuleIsLoadBearing"];
        var loadBearing = map
            .Where(row => markers.Any(m => row.Test.Contains(m, StringComparison.Ordinal)))
            .ToList();
        loadBearing.Should().NotBeEmpty();
        foreach (var (test, packRule) in loadBearing)
        {
            if (string.Equals(packRule, "stays in C#", StringComparison.Ordinal) || string.Equals(packRule, "not needed", StringComparison.Ordinal))
            {
                continue;
            }

            PackRuleIds(packRule).Should().NotBeEmpty().And.BeSubsetOf(requiringTarget, $"{test} guards the target of the rules it maps to");
        }

        string Cell(string prefix) =>
            map.Should().ContainSingle(row => row.Test.StartsWith(prefix, StringComparison.Ordinal), $"the map has one row for {prefix}").Which.PackRule;

        Cell("MemoryRagNetAdapter_DoesDependOn_RagNet_RuleIsLoadBearing").Should().Be("stays in C#");
        Cell("SkillsAssembly_IsLoaded").Should().Be("not needed");
        Cell("ChannelsAssemblies_AreLoaded").Should().Be("not needed");
        foreach (var prefix in new[]
                 {
                     "AgentsProject_DoesDependOn_Thalos", "SubagentRunnerType_IsLoaded", "ZeroAllocOrmAssembly_IsLoaded",
                     "ZeroAllocOutboxAssembly_IsLoaded", "CronosAssembly_IsLoaded", "ApplicationDtoNames_Cover",
                 })
        {
            PackRuleIds(Cell(prefix)).Should().NotBeEmpty().And.BeSubsetOf(requiringTarget, $"{prefix} maps to rules with requireTarget: true");
        }

        var uniqueNames = rules.Values.Where(static r => r.Children.ContainsKey(new YamlScalarNode("uniqueNames"))).ToList();
        uniqueNames.Should().ContainSingle();
        var against = (YamlMappingNode)((YamlMappingNode)uniqueNames[0].Children[new YamlScalarNode("uniqueNames")]).Children[new YamlScalarNode("against")];
        against.Children.Keys.Cast<YamlScalarNode>().Select(static k => k.Value).Should().Equal("namespaces");
        ((YamlSequenceNode)against.Children[new YamlScalarNode("namespaces")]).Children
            .Cast<YamlScalarNode>().Select(static n => n.Value).Should().Equal("Daedalus.Application.DTOs.**");
    }
}
