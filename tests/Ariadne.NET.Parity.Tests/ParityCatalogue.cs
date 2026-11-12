using Ariadne.NET.Tests.Shared;

namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// Reads the ArchUnitNET parity catalogue, <c>docs-site/docs/archunitnet-parity.md</c>: the rows of every table whose header is
/// <c>| Id | ArchUnitNET | Ariadne | Status | Notes |</c>, and the case ids linked under <c>## Divergences</c>.
/// </summary>
internal static class ParityCatalogue
{
    private const string DivergencesHeading = "## Divergences";

    private static readonly string[] Header = ["Id", "ArchUnitNET", "Ariadne", "Status", "Notes"];

    /// <summary>The catalogue page in this repository.</summary>
    public static string PagePath { get; } =
        Path.GetFullPath(Path.Combine(FixtureProjects.Root, "..", "..", "docs-site", "docs", "archunitnet-parity.md"));

    /// <summary>The catalogue rows of the page at <paramref name="path"/>.</summary>
    public static IReadOnlyList<CatalogueRow> Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>
    /// The rows of every catalogue table in <paramref name="markdown"/>, in page order. Backticks are stripped from <c>Id</c>
    /// and <c>Status</c>; the other cells are kept as written. Tables with another header are ignored.
    /// </summary>
    public static IReadOnlyList<CatalogueRow> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var rows = new List<CatalogueRow>();
        var inCatalogueTable = false;
        var expectSeparator = false;
        foreach (var rawLine in Lines(markdown))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith('|'))
            {
                inCatalogueTable = false;
                expectSeparator = false;
                continue;
            }

            var cells = Cells(line);
            if (!inCatalogueTable)
            {
                inCatalogueTable = cells.SequenceEqual(Header, StringComparer.Ordinal);
                expectSeparator = inCatalogueTable;
                continue;
            }

            if (expectSeparator)
            {
                expectSeparator = false;
                continue;
            }

            if (cells.Count != Header.Length)
            {
                throw new FormatException($"A catalogue row needs {Header.Length} cells but has {cells.Count}: {line}");
            }

            rows.Add(new CatalogueRow(StripBackticks(cells[0]), cells[1], cells[2], StripBackticks(cells[3]), cells[4]));
        }

        return rows;
    }

    /// <summary>
    /// The case ids that <paramref name="markdown"/> links under its <c>## Divergences</c> heading, up to the next level-two
    /// heading: the text of every link <c>[id](#anchor)</c> whose text is a case id, such as <c>c-x</c> or <c>c-x:not</c>.
    /// </summary>
    public static IReadOnlySet<string> DivergenceLinks(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var inSection = false;
        foreach (var rawLine in Lines(markdown))
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = string.Equals(line, DivergencesHeading, StringComparison.Ordinal);
                continue;
            }

            if (inSection)
            {
                foreach (var text in LinkTexts(line))
                {
                    if (IsCaseId(text))
                    {
                        ids.Add(text);
                    }
                }
            }
        }

        return ids;
    }

    private static string[] Lines(string markdown) => markdown.Split('\n');

    private static List<string> Cells(string line)
    {
        var inner = line.Trim('|');
        return [.. inner.Split('|').Select(c => c.Trim())];
    }

    private static string StripBackticks(string cell) => cell.Replace("`", string.Empty, StringComparison.Ordinal).Trim();

    private static IEnumerable<string> LinkTexts(string line)
    {
        var start = 0;
        while (true)
        {
            var open = line.IndexOf('[', start);
            if (open < 0)
            {
                yield break;
            }

            var close = line.IndexOf("](#", open, StringComparison.Ordinal);
            if (close < 0)
            {
                yield break;
            }

            var text = line[(open + 1)..close];
            yield return text[(text.LastIndexOf('[') + 1)..];
            start = close + 3;
        }
    }

    private static bool IsCaseId(string text)
    {
        var id = text.EndsWith(":not", StringComparison.Ordinal) ? text[..^4] : text;
        return id.Length > 2
            && id[0] is 'p' or 'c' or 's' or 'x'
            && id[1] == '-'
            && id[2..].All(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');
    }
}
