using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class SolutionDiscoveryTests
{
    private static void Touch(TempProject project, string relative)
    {
        var path = Path.Combine(project.Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
    }

    [Fact]
    public void One_slnx_in_the_root_is_found()
    {
        using var project = new TempProject();
        Touch(project, "Shop.slnx");

        SolutionDiscovery.Find(project.Root).Should().Be(Path.Combine(project.Root, "Shop.slnx"));
    }

    [Fact]
    public void A_solution_in_src_is_found_when_the_root_has_none()
    {
        using var project = new TempProject();
        Touch(project, "src/Shop.sln");

        SolutionDiscovery.Find(project.Root).Should().Be(Path.Combine(project.Root, "src", "Shop.sln"));
    }

    [Fact]
    public void The_root_wins_over_src()
    {
        using var project = new TempProject();
        Touch(project, "Root.slnx");
        Touch(project, "src/Other.slnx");

        SolutionDiscovery.Find(project.Root).Should().Be(Path.Combine(project.Root, "Root.slnx"));
    }

    [Fact]
    public void Slnx_wins_over_sln()
    {
        using var project = new TempProject();
        Touch(project, "Shop.sln");
        Touch(project, "Shop.slnx");

        SolutionDiscovery.Find(project.Root).Should().Be(Path.Combine(project.Root, "Shop.slnx"));
    }

    [Fact]
    public void Slnx_wins_over_slnf()
    {
        using var project = new TempProject();
        Touch(project, "Part.slnf");
        Touch(project, "Shop.slnx");

        SolutionDiscovery.Find(project.Root).Should().Be(Path.Combine(project.Root, "Shop.slnx"));
    }

    [Fact]
    public void Sln_wins_over_slnf()
    {
        using var project = new TempProject();
        Touch(project, "Part.slnf");
        Touch(project, "Shop.sln");

        SolutionDiscovery.Find(project.Root).Should().Be(Path.Combine(project.Root, "Shop.sln"));
    }

    [Fact]
    public void A_lone_slnf_is_found()
    {
        using var project = new TempProject();
        Touch(project, "Part.slnf");

        SolutionDiscovery.Find(project.Root).Should().Be(Path.Combine(project.Root, "Part.slnf"));
    }

    [Fact]
    public void Two_slnx_files_are_ambiguous()
    {
        using var project = new TempProject();
        Touch(project, "A.slnx");
        Touch(project, "B.slnx");
        Touch(project, "src/C.slnx");

        SolutionDiscovery.Find(project.Root).Should().BeNull();
    }

    [Fact]
    public void No_solution_anywhere_gives_null()
    {
        using var project = new TempProject();
        Touch(project, "src/readme.txt");

        SolutionDiscovery.Find(project.Root).Should().BeNull();
    }

    [Fact]
    public void A_missing_root_gives_null() =>
        SolutionDiscovery.Find(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).Should().BeNull();

    [Fact]
    public void Extensions_match_regardless_of_case()
    {
        using var project = new TempProject();
        Touch(project, "Shop.SLNX");

        SolutionDiscovery.Find(project.Root).Should().Be(Path.Combine(project.Root, "Shop.SLNX"));
    }
}
