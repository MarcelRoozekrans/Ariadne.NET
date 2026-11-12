using FluentAssertions;

namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// Checks the catalogue page against <see cref="ParityCases"/>, <see cref="KnownGaps"/> and <see cref="Divergences"/>, so a
/// row cannot claim a status its cases do not back.
/// </summary>
public sealed class CatalogueConsistencyTests
{
    private static readonly HashSet<string> InScope = new(StringComparer.Ordinal) { "covered", "gap", "divergence" };

    private static readonly HashSet<string> NoCases = new(StringComparer.Ordinal) { "deferred", "out-of-scope", "ariadne-only" };

    private static IReadOnlyList<CatalogueRow> Rows => ParityCatalogue.Load(ParityCatalogue.PagePath);

    [Fact]
    public void Row_ids_are_unique()
    {
        Rows.Select(r => r.Id).Should().OnlyHaveUniqueItems("a row id names one row, which its cases and links rely on");
    }

    [Fact]
    public void Every_in_scope_row_has_a_case_and_every_case_a_row()
    {
        var inScopeRows = Rows.Where(r => InScope.Contains(r.Status)).Select(r => r.Id);
        var caseRows = ParityCases.All.Select(c => RowId(c.Id)).Distinct(StringComparer.Ordinal);

        caseRows.Should().BeEquivalentTo(inScopeRows, "every covered, gap or divergence row needs a case, and every case a row");
    }

    [Fact]
    public void Row_status_follows_from_its_cases()
    {
        var casesByRow = ParityCases.All.ToLookup(c => RowId(c.Id), c => c.Id, StringComparer.Ordinal);
        var gaps = KnownGaps.Entries.ToDictionary(g => g.CaseId, StringComparer.Ordinal);
        var divergent = Divergences.Entries.Select(d => d.CaseId).ToHashSet(StringComparer.Ordinal);

        foreach (var row in Rows.Where(r => InScope.Contains(r.Status)))
        {
            var hasGap = false;
            var diverges = false;
            foreach (var caseId in casesByRow[row.Id])
            {
                diverges |= divergent.Contains(caseId);
                if (gaps.TryGetValue(caseId, out var gap))
                {
                    hasGap = true;
                    row.Notes.Should().Contain(gap.Phase, "row {0} must name the phase of its gap case {1}", row.Id, caseId);
                }
            }

            var expected = hasGap ? "gap" : diverges ? "divergence" : "covered";
            row.Status.Should().Be(expected, "row {0} has cases {1}", row.Id, string.Join(", ", casesByRow[row.Id]));
        }
    }

    [Fact]
    public void Every_divergence_is_linked_on_the_page()
    {
        var links = ParityCatalogue.DivergenceLinks(File.ReadAllText(ParityCatalogue.PagePath));

        links.Should().BeEquivalentTo(Divergences.Entries.Select(d => d.CaseId), "the Divergences section lists every entry and no other");
    }

    [Fact]
    public void Every_divergence_is_linked_from_its_row()
    {
        // A divergence on a row whose status is gap would otherwise be invisible in the table.
        var rows = Rows;
        foreach (var divergence in Divergences.Entries)
        {
            var id = RowId(divergence.CaseId);
            var row = rows.Should().ContainSingle(r => string.Equals(r.Id, id, StringComparison.Ordinal)).Subject;
            row.Notes.Should().Contain($"(#{id})", "row {0} must link its divergence", id);
        }
    }

    [Fact]
    public void Entries_name_existing_cases()
    {
        var ids = ParityCases.All.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        KnownGaps.Entries.Select(g => g.CaseId).Should().OnlyContain(id => ids.Contains(id));
        Divergences.Entries.Select(d => d.CaseId).Should().OnlyContain(id => ids.Contains(id));
    }

    [Fact]
    public void No_case_for_out_of_scope_rows()
    {
        var noCaseRows = Rows.Where(r => NoCases.Contains(r.Status)).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);

        ParityCases.All.Select(c => c.Id).Should().NotContain(id => noCaseRows.Contains(RowId(id)));
    }

    private static string RowId(string caseId) => caseId.EndsWith(":not", StringComparison.Ordinal) ? caseId[..^4] : caseId;
}
