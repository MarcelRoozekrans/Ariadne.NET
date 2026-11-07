using System.Text.Json;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class PreToolUseHookTests
{
    private static PreToolUseHook Create()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        return new PreToolUseHook(new ProjectRules(loader, new RulePackResolver(registry, loader, new RulePackMerger())));
    }

    private const string Rules = "schema: 2\nname: shop\nlayers:\n  - name: Domain\n    id: SHOP-DOMAIN\n    because: Domain.\n    assemblies: [\"Shop.Domain\"]\n";

    private static TempProject ProjectWith(string projectName)
    {
        var project = new TempProject();
        project.WriteRules(Rules);
        var dir = Path.Combine(project.Root, projectName);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{projectName}.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        return project;
    }

    private static Task<string?> RunAsync(string root, string session, string? tool, string? file, string? content = "namespace X;") =>
        Create().RunAsync(
            new HookInput { SessionId = session, Cwd = root, ToolName = tool, FilePath = file, Content = content },
            TextWriter.Null,
            CancellationToken.None);

    [Fact]
    public void Serves_the_pre_tool_use_event() => Create().Event.Should().Be("pre-tool-use");

    [Fact]
    public async Task A_new_cs_file_in_an_unselected_project_returns_an_ask_document()
    {
        using var project = ProjectWith("Shop.Web");
        var session = Guid.NewGuid().ToString("N");
        var file = Path.Combine(project.Root, "Shop.Web", "Home.cs");

        var json = await RunAsync(project.Root, session, "Write", file).ConfigureAwait(true);

        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        var output = doc.RootElement.GetProperty("hookSpecificOutput");
        output.GetProperty("hookEventName").GetString().Should().Be("PreToolUse");
        output.GetProperty("permissionDecision").GetString().Should().Be("ask");
        output.GetProperty("permissionDecisionReason").GetString().Should().Contain("Shop.Web").And.Contain("Domain");
        EditedFiles.Clear(session);
    }

    [Fact]
    public async Task A_new_cs_file_in_a_selected_project_returns_null_but_is_recorded()
    {
        using var project = ProjectWith("Shop.Domain");
        var session = Guid.NewGuid().ToString("N");
        var file = Path.Combine(project.Root, "Shop.Domain", "Order.cs");

        var json = await RunAsync(project.Root, session, "Write", file).ConfigureAwait(true);

        json.Should().BeNull();
        EditedFiles.Read(session).Should().Equal(file);
        EditedFiles.Clear(session);
    }

    [Fact]
    public async Task The_same_file_twice_records_one_path()
    {
        using var project = ProjectWith("Shop.Web");
        var session = Guid.NewGuid().ToString("N");
        var file = Path.Combine(project.Root, "Shop.Web", "Home.cs");

        await RunAsync(project.Root, session, "Edit", file).ConfigureAwait(true);
        await RunAsync(project.Root, session, "MultiEdit", file).ConfigureAwait(true);

        EditedFiles.Read(session).Should().Equal(file);
        EditedFiles.Clear(session);
    }

    [Fact]
    public async Task Another_tool_returns_null_and_records_nothing()
    {
        using var project = ProjectWith("Shop.Web");
        var session = Guid.NewGuid().ToString("N");

        var json = await RunAsync(project.Root, session, "Bash", Path.Combine(project.Root, "Shop.Web", "Home.cs")).ConfigureAwait(true);

        json.Should().BeNull();
        EditedFiles.Read(session).Should().BeEmpty();
    }

    [Fact]
    public async Task A_markdown_file_records_nothing()
    {
        using var project = ProjectWith("Shop.Web");
        var session = Guid.NewGuid().ToString("N");

        var json = await RunAsync(project.Root, session, "Write", Path.Combine(project.Root, "Shop.Web", "Readme.md")).ConfigureAwait(true);

        json.Should().BeNull();
        EditedFiles.Read(session).Should().BeEmpty();
    }

    [Fact]
    public async Task A_missing_rules_file_returns_null()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ariadne-hooks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var json = await RunAsync(root, Guid.NewGuid().ToString("N"), "Write", Path.Combine(root, "A.cs")).ConfigureAwait(true);
            json.Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
