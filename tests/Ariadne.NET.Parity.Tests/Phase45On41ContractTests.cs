using FluentAssertions;

namespace Ariadne.NET.Parity.Tests;

/// <summary>
/// Pins the names Phase 4.5 builds on from Phase 4.1: the catalogue page path, its loader and the row shape. Each name is bound
/// to a typed local, so a rename fails to compile.
/// </summary>
public sealed class Phase45On41ContractTests
{
    [Fact]
    public void Names_phase_4_5_builds_on_exist()
    {
        string path = ParityCatalogue.PagePath;
        Func<string, IReadOnlyList<CatalogueRow>> load = ParityCatalogue.Load;

        var row = new CatalogueRow("p-x", "`X`", "`types`", "covered", "notes");
        (row.Id, row.ArchUnitNet, row.Ariadne, row.Status).Should().Be(("p-x", "`X`", "`types`", "covered"));

        path.Replace('\\', '/').Should().EndWith("docs-site/docs/archunitnet-parity.md");
        load(path).Should().HaveCountGreaterThanOrEqualTo(30);
        File.ReadAllLines(path).Should().Contain("## Divergences");
    }
}
