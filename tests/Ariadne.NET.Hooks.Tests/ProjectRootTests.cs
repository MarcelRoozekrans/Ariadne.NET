using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class ProjectRootTests
{
    private static string Sub(TempProject project, params string[] parts)
    {
        var dir = Path.Combine([project.Root, .. parts]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void A_folder_with_rules_is_its_own_root()
    {
        using var project = new TempProject();
        project.WriteRules();

        ProjectRoot.Resolve(project.Root, _ => null).Should().Be(project.Root);
    }

    [Fact]
    public void A_subfolder_resolves_to_the_nearest_folder_with_rules()
    {
        using var project = new TempProject();
        project.WriteRules();
        var deep = Sub(project, "src", "Shop.Core");

        ProjectRoot.Resolve(deep, _ => null).Should().Be(project.Root);
    }

    [Fact]
    public void The_nearest_rules_win_over_an_outer_project()
    {
        using var project = new TempProject();
        project.WriteRules();
        var inner = Sub(project, "inner");
        Directory.CreateDirectory(Path.Combine(inner, ".architecture"));
        File.WriteAllText(Path.Combine(inner, ".architecture", "rules.yaml"), "schema: 2\nname: inner\n");

        ProjectRoot.Resolve(Sub(project, "inner", "src"), _ => null).Should().Be(inner);
    }

    [Fact]
    public void Rules_beat_the_environment_variable()
    {
        using var project = new TempProject();
        project.WriteRules();
        using var other = new TempProject();
        other.WriteRules();

        ProjectRoot.Resolve(project.Root, name => string.Equals(name, "CLAUDE_PROJECT_DIR", StringComparison.Ordinal) ? other.Root : null).Should().Be(project.Root);
    }

    [Fact]
    public void Outside_any_project_the_environment_variable_names_the_root()
    {
        using var outside = new TempProject();
        using var project = new TempProject();
        project.WriteRules();

        ProjectRoot.Resolve(outside.Root, name => string.Equals(name, "CLAUDE_PROJECT_DIR", StringComparison.Ordinal) ? project.Root : null).Should().Be(project.Root);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/no/such/folder/for/ariadne")]
    public void An_unusable_environment_variable_falls_back_to_the_cwd(string? value)
    {
        using var outside = new TempProject();

        ProjectRoot.Resolve(outside.Root, _ => value).Should().Be(outside.Root);
    }

    [Fact]
    public void Nothing_found_falls_back_to_the_cwd()
    {
        using var outside = new TempProject();

        ProjectRoot.Resolve(outside.Root, _ => null).Should().Be(outside.Root);
    }

    [Fact]
    public void A_missing_cwd_stands_for_the_process_directory()
    {
        ProjectRoot.Resolve(null, _ => null).Should().NotBeNullOrEmpty();
    }
}
