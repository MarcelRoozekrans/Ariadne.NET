using Ariadne.NET.Sync;
using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class CacheFreshnessTests
{
    private readonly ImportsYamlLoader _loader = new();

    [Fact]
    public void A_cached_tag_inside_the_range_gives_no_warning()
    {
        using var project = new TempProject();
        project.WriteImports("shared", "^1.0.0");
        project.WriteCache("shared", "v1.2.0");

        CacheFreshness.Check(project.Root, _loader).Should().BeEmpty();
    }

    [Fact]
    public void A_cached_tag_outside_the_range_warns_with_the_name_and_the_sync_command()
    {
        using var project = new TempProject();
        project.WriteImports("shared", "^1.0.0");
        project.WriteCache("shared", "v2.0.0");

        var warnings = CacheFreshness.Check(project.Root, _loader);

        warnings.Should().ContainSingle().Which.Should().Contain("shared").And.Contain("run 'ariadne sync'");
    }

    [Fact]
    public void A_missing_cache_directory_warns()
    {
        using var project = new TempProject();
        project.WriteImports("shared", "^1.0.0");

        CacheFreshness.Check(project.Root, _loader).Should().ContainSingle().Which.Should().Contain("shared");
    }

    [Fact]
    public void A_missing_git_info_file_warns()
    {
        using var project = new TempProject();
        project.WriteImports("shared", "^1.0.0");
        Directory.CreateDirectory(Path.Combine(project.Root, ".architecture", ".cache", "git", "shared"));

        CacheFreshness.Check(project.Root, _loader).Should().ContainSingle();
    }

    [Fact]
    public void A_corrupt_git_info_file_warns()
    {
        using var project = new TempProject();
        project.WriteImports("shared", "^1.0.0");
        var dir = Path.Combine(project.Root, ".architecture", ".cache", "git", "shared");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".git-info.json"), "{ not json");

        CacheFreshness.Check(project.Root, _loader).Should().ContainSingle();
    }

    [Fact]
    public void No_imports_file_gives_no_warning()
    {
        using var project = new TempProject();

        CacheFreshness.Check(project.Root, _loader).Should().BeEmpty();
        CacheFreshness.Versions(project.Root, _loader).Should().BeEmpty();
    }

    [Fact]
    public void An_unreadable_manifest_is_one_warning()
    {
        using var project = new TempProject();
        File.WriteAllText(Path.Combine(project.Root, ".architecture", "imports.yaml"), "imports: [");

        CacheFreshness.Check(project.Root, _loader).Should().ContainSingle().Which.Should().Contain("imports.yaml");
    }

    [Fact]
    public void Versions_lists_name_at_tag_for_cached_imports_only()
    {
        using var project = new TempProject();
        project.WriteImports("shared", "^1.0.0");
        project.WriteCache("shared", "v1.2.0");

        CacheFreshness.Versions(project.Root, _loader).Should().Equal("shared@v1.2.0");
    }
}
