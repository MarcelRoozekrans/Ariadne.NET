using FluentAssertions;

namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// Checks the migration guide's construct map against the parity catalogue: every id the map names is a catalogue row,
/// and every catalogue row is mapped exactly once.
/// </summary>
public sealed class MigrationGuideTests
{
    private static readonly string[] MapHeader = ["ArchUnitNET", "Pack YAML", "Catalogue id"];

    private static string GuidePath { get; } =
        Path.Combine(Path.GetDirectoryName(ParityCatalogue.PagePath)!, "migrating-from-archunitnet.md");

    /// <summary>
    /// Every code-font id in the <c>Catalogue id</c> column of every table whose header is exactly
    /// <c>| ArchUnitNET | Pack YAML | Catalogue id |</c>, in page order, duplicates kept.
    /// </summary>
    internal static IReadOnlyList<string> CatalogueIds(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var ids = new List<string>();
        var inMap = false;
        var expectSeparator = false;
        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith('|'))
            {
                inMap = false;
                expectSeparator = false;
                continue;
            }

            var cells = Cells(line);
            if (!inMap)
            {
                inMap = cells.SequenceEqual(MapHeader, StringComparer.Ordinal);
                expectSeparator = inMap;
                continue;
            }

            if (expectSeparator)
            {
                expectSeparator = false;
                continue;
            }

            if (cells.Count != MapHeader.Length)
            {
                throw new FormatException($"A construct-map row needs {MapHeader.Length} cells but has {cells.Count}: {line}");
            }

            ids.AddRange(CodeSpans(cells[2]));
        }

        return ids;
    }

    [Fact]
    public void CatalogueIds_reads_only_map_tables()
    {
        const string Sample = """
            | ArchUnitNET | Pack YAML | Catalogue id |
            |---|---|---|
            | `A` | `a` | `p-a`, `p-a` |
            | `B` | `b` | `c-b` |

            | Something | Catalogue id |
            |---|---|
            | `C` | `x-c` |
            """;

        CatalogueIds(Sample).Should().Equal("p-a", "p-a", "c-b");
    }

    [Fact]
    public void Every_catalogue_id_on_the_guide_exists()
    {
        var rowIds = ParityCatalogue.Load(ParityCatalogue.PagePath).Select(static r => r.Id).ToHashSet(StringComparer.Ordinal);

        var guideIds = CatalogueIds(File.ReadAllText(GuidePath));

        guideIds.Should().NotBeEmpty();
        guideIds.Where(id => !rowIds.Contains(id)).Should().BeEmpty("every id the guide names must be a catalogue row");
    }

    [Fact]
    public void Every_catalogue_row_is_mapped_on_the_guide()
    {
        var guideIds = CatalogueIds(File.ReadAllText(GuidePath));
        var counts = guideIds.GroupBy(static id => id, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.Ordinal);

        var wrong = ParityCatalogue.Load(ParityCatalogue.PagePath)
            .Select(static r => r.Id)
            .Where(id => !counts.TryGetValue(id, out var n) || n != 1)
            .Select(id => $"{id} appears {(counts.TryGetValue(id, out var n) ? n : 0)} time(s)")
            .ToList();

        wrong.Should().BeEmpty("every catalogue row appears in exactly one construct-map row");
    }

    private static List<string> Cells(string line)
    {
        var start = line.StartsWith('|') ? 1 : 0;
        var end = line.Length > start && line.EndsWith('|') ? line.Length - 1 : line.Length;
        return [.. line[start..end].Split('|').Select(static c => c.Trim())];
    }

    private static IEnumerable<string> CodeSpans(string cell)
    {
        var parts = cell.Split('`');
        for (var i = 1; i < parts.Length; i += 2)
        {
            yield return parts[i];
        }
    }
}
