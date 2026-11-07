using System.Text.Json;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class PreToolUseHookTests
{
    private const string Rules = "schema: 2\nname: shop\nlayers:\n  - name: Domain\n    id: SHOP-DOMAIN\n    because: Domain.\n    assemblies: [\"Shop.Domain\"]\n";
    private const string NamespaceRules = "schema: 2\nname: shop\nlayers:\n  - name: Ok\n    id: SHOP-OK\n    because: Ok.\n    namespaces: [\"Shop.Ok.**\"]\n";

    private static PreToolUseHook Create()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        return new PreToolUseHook(new ProjectRules(loader, new RulePackResolver(registry, loader, new RulePackMerger())), _ => null);
    }

    private static TempProject ProjectWith(string projectName, string rules = Rules)
    {
        var project = new TempProject();
        project.WriteRules(rules);
        var dir = Path.Combine(project.Root, projectName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{projectName}.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        return project;
    }

    private static Task<string?> RunAsync(string root, string session, string? tool, string? file, string? content = "namespace X;", TextWriter? warn = null) =>
        Create().RunAsync(
            new HookInput { SessionId = session, Cwd = root, ToolName = tool, FilePath = file, Content = content },
            warn ?? TextWriter.Null,
            CancellationToken.None);

    private static string[] Lines(StringWriter writer) =>
        writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [Fact]
    public async Task After_a_cd_into_the_project_the_hook_still_asks_for_a_new_file()
    {
        using var project = ProjectWith("Shop.Web");
        using var session = new TestSession();
        var below = Path.Combine(project.Root, "Shop.Web");
        var file = Path.Combine(below, "Home.cs");

        var json = await RunAsync(below, session.Id, "Write", file).ConfigureAwait(true);

        json.Should().NotBeNull().And.Contain("\"ask\"");
    }

    [Fact]
    public async Task A_relative_path_is_taken_relative_to_the_cwd_and_recorded_absolute()
    {
        using var project = ProjectWith("Shop.Web");
        using var session = new TestSession();
        var below = Path.Combine(project.Root, "Shop.Web");

        var json = await RunAsync(below, session.Id, "Write", "Home.cs").ConfigureAwait(true);

        json.Should().NotBeNull().And.Contain("\"ask\"");
        EditedFiles.Read(session.Id).Should().Equal(Path.Combine(below, "Home.cs"));
    }

    [Fact]
    public void Serves_the_pre_tool_use_event() => Create().Event.Should().Be("pre-tool-use");

    [Fact]
    public async Task A_new_cs_file_in_an_unselected_project_returns_an_ask_document()
    {
        using var project = ProjectWith("Shop.Web");
        using var session = new TestSession();
        var file = Path.Combine(project.Root, "Shop.Web", "Home.cs");

        var json = await RunAsync(project.Root, session.Id, "Write", file).ConfigureAwait(true);

        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        var output = doc.RootElement.GetProperty("hookSpecificOutput");
        output.GetProperty("hookEventName").GetString().Should().Be("PreToolUse");
        output.GetProperty("permissionDecision").GetString().Should().Be("ask");
        output.GetProperty("permissionDecisionReason").GetString().Should().Contain("Shop.Web").And.Contain("Domain");
    }

    [Fact]
    public async Task A_new_cs_file_in_a_selected_project_returns_null_but_is_recorded()
    {
        using var project = ProjectWith("Shop.Domain");
        using var session = new TestSession();
        var file = Path.Combine(project.Root, "Shop.Domain", "Order.cs");

        var json = await RunAsync(project.Root, session.Id, "Write", file).ConfigureAwait(true);

        json.Should().BeNull();
        EditedFiles.Read(session.Id).Should().Equal(file);
    }

    [Fact]
    public async Task The_same_file_twice_records_one_path()
    {
        using var project = ProjectWith("Shop.Web");
        using var session = new TestSession();
        var file = Path.Combine(project.Root, "Shop.Web", "Home.cs");

        await RunAsync(project.Root, session.Id, "Edit", file).ConfigureAwait(true);
        await RunAsync(project.Root, session.Id, "MultiEdit", file).ConfigureAwait(true);

        EditedFiles.Read(session.Id).Should().Equal(file);
    }

    [Theory]
    [InlineData("Edit")]
    [InlineData("MultiEdit")]
    public async Task Editing_an_existing_file_is_never_questioned_but_is_recorded(string tool)
    {
        using var project = ProjectWith("Shop.Web");
        using var session = new TestSession();
        var file = Path.Combine(project.Root, "Shop.Web", "Home.cs");
        File.WriteAllText(file, "class A;");

        var json = await RunAsync(project.Root, session.Id, tool, file).ConfigureAwait(true);

        json.Should().BeNull();
        EditedFiles.Read(session.Id).Should().Equal(file);
    }

    [Fact]
    public async Task Writing_over_an_existing_file_is_never_questioned()
    {
        using var project = ProjectWith("Shop.Web");
        using var session = new TestSession();
        var file = Path.Combine(project.Root, "Shop.Web", "Home.cs");
        File.WriteAllText(file, "class A;");

        (await RunAsync(project.Root, session.Id, "Write", file).ConfigureAwait(true)).Should().BeNull();
    }

    [Fact]
    public async Task Edit_ignores_content_and_Write_uses_it()
    {
        using var project = ProjectWith("Shop.Web", NamespaceRules);
        using var session = new TestSession();
        var file = Path.Combine(project.Root, "Shop.Web", "Home.cs");
        const string Content = "namespace Shop.Ok.Pages;";

        (await RunAsync(project.Root, session.Id, "Write", file, Content).ConfigureAwait(true)).Should().BeNull();
        (await RunAsync(project.Root, session.Id, "Edit", file, Content).ConfigureAwait(true)).Should().NotBeNull();
    }

    [Fact]
    public async Task Another_tool_returns_null_and_records_nothing()
    {
        using var project = ProjectWith("Shop.Web");
        using var session = new TestSession();

        var json = await RunAsync(project.Root, session.Id, "Bash", Path.Combine(project.Root, "Shop.Web", "Home.cs")).ConfigureAwait(true);

        json.Should().BeNull();
        EditedFiles.Read(session.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task A_markdown_file_records_nothing()
    {
        using var project = ProjectWith("Shop.Web");
        using var session = new TestSession();

        var json = await RunAsync(project.Root, session.Id, "Write", Path.Combine(project.Root, "Shop.Web", "Readme.md")).ConfigureAwait(true);

        json.Should().BeNull();
        EditedFiles.Read(session.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task A_missing_rules_file_returns_null_without_a_warning()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ariadne-hooks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        using var session = new TestSession();
        using var warn = new StringWriter();
        try
        {
            var json = await RunAsync(root, session.Id, "Write", Path.Combine(root, "A.cs"), warn: warn).ConfigureAwait(true);

            json.Should().BeNull();
            warn.ToString().Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Invalid_rules_return_null_with_one_warning()
    {
        using var project = ProjectWith("Shop.Web", "schema: 2\nname: shop\nextends:\n  - builtin://does-not-exist\n");
        using var session = new TestSession();
        using var warn = new StringWriter();

        var json = await RunAsync(project.Root, session.Id, "Write", Path.Combine(project.Root, "Shop.Web", "Home.cs"), warn: warn).ConfigureAwait(true);

        json.Should().BeNull();
        Lines(warn).Should().ContainSingle().Which.Should().Contain("could not load the project rules");
    }

    [Fact]
    public async Task A_recording_failure_still_returns_the_question_with_one_warning()
    {
        using var project = ProjectWith("Shop.Web");
        var longSession = new string('a', 400);
        using var warn = new StringWriter();

        var json = await RunAsync(project.Root, longSession, "Write", Path.Combine(project.Root, "Shop.Web", "Home.cs"), warn: warn).ConfigureAwait(true);

        json.Should().NotBeNull().And.Contain("\"ask\"");
        Lines(warn).Should().ContainSingle().Which.Should().Contain("could not record the edited file");
    }
}
