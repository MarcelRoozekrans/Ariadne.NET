using FluentAssertions;
using Xunit;

namespace Ariadne.NET.Parity.Tests;

public sealed class ParityCatalogueTests
{
    private static readonly string[] Statuses = ["covered", "gap", "divergence", "deferred", "out-of-scope", "ariadne-only"];

    [Fact]
    public void Parse_reads_rows_of_catalogue_tables_only()
    {
        const string markdown = """
            # Sample

            | Id | ArchUnitNET | Ariadne | Status | Notes |
            |---|---|---|---|---|
            | `p-reside-in-namespace` | `ResideInNamespace` | `namespaces` | `covered` | |
            | `c-be-sealed` | `BeSealed` | — | `gap` | 4.2 |

            | Exit code | Meaning |
            |---|---|
            | 0 | No violations. |
            """;

        var rows = ParityCatalogue.Parse(markdown);

        rows.Should().HaveCount(2);
        rows[0].Id.Should().Be("p-reside-in-namespace");
        rows[0].ArchUnitNet.Should().Be("`ResideInNamespace`");
        rows[0].Ariadne.Should().Be("`namespaces`");
        rows[0].Status.Should().Be("covered");
        rows[0].Notes.Should().BeEmpty();
        rows[1].Id.Should().Be("c-be-sealed");
        rows[1].Status.Should().Be("gap");
        rows[1].Notes.Should().Be("4.2");
    }

    [Fact]
    public void DivergenceLinks_reads_ids_under_the_heading()
    {
        const string markdown = """
            | Id | ArchUnitNET | Ariadne | Status | Notes |
            |---|---|---|---|---|
            | `c-y` | `Y` | — | `divergence` | [c-y](#c-y) |

            ## Divergences

            ### c-x

            See [c-x](#c-x) and [c-x:not](#c-x-not).

            ## After

            [c-z](#c-z)
            """;

        ParityCatalogue.DivergenceLinks(markdown).Should().BeEquivalentTo(["c-x", "c-x:not"]);
    }

    [Fact]
    public void Real_page_parses_and_uses_only_known_statuses()
    {
        var rows = ParityCatalogue.Load(ParityCatalogue.PagePath);

        rows.Should().HaveCountGreaterThanOrEqualTo(30);
        rows.Select(r => r.Id).Should().OnlyHaveUniqueItems();
        rows.Should().AllSatisfy(r =>
        {
            r.Id.Should().MatchRegex("^[pcsx]-[a-z0-9-]+$");
            Statuses.Should().Contain(r.Status, "row {0} must use a known status", r.Id);
            NotesMatchStatus(r).Should().BeTrue("row {0} with status {1} has notes '{2}'", r.Id, r.Status, r.Notes);
        });
        rows.Should().NotContain(r => r.Notes.Contains("#<issue>", StringComparison.Ordinal));
        File.ReadAllLines(ParityCatalogue.PagePath).Should().Contain("## Divergences");
    }

    [Fact]
    public void Parse_keeps_an_empty_last_cell_written_without_spaces()
    {
        const string markdown = """
            | Id | ArchUnitNET | Ariadne | Status | Notes |
            |---|---|---|---|---|
            | `p-x` | `X` | `types` | `covered` ||
            """;

        var row = ParityCatalogue.Parse(markdown).Should().ContainSingle().Subject;

        row.Status.Should().Be("covered");
        row.Notes.Should().BeEmpty();
    }

    // gap names its phase, deferred links an issue, out-of-scope gives a reason.
    private static bool NotesMatchStatus(CatalogueRow row) => row.Status switch
    {
        "gap" => row.Notes.StartsWith("4.2", StringComparison.Ordinal)
            || row.Notes.StartsWith("4.3", StringComparison.Ordinal)
            || row.Notes.StartsWith("unplanned", StringComparison.Ordinal),
        "deferred" => HasIssueLink(row.Notes),
        "out-of-scope" => row.Notes.Length > 0,
        _ => true,
    };

    private static bool HasIssueLink(string notes)
    {
        for (var i = notes.IndexOf('#', StringComparison.Ordinal); i >= 0; i = notes.IndexOf('#', i + 1))
        {
            if (i + 1 < notes.Length && char.IsAsciiDigit(notes[i + 1]))
            {
                return true;
            }
        }

        return false;
    }
}
