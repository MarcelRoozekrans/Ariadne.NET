using System.Text.Json;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Sync;
using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class SessionStartHookTests
{
    private static SessionStartHook Create()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        var rules = new ProjectRules(loader, new RulePackResolver(registry, loader, new RulePackMerger()));
        return new SessionStartHook(rules, new ImportsYamlLoader());
    }

    private static async Task<string> ContextAsync(string root, string? source = null)
    {
        var input = new HookInput { SessionId = "s1", Cwd = root, Source = source };
        var json = await Create().RunAsync(input, TextWriter.Null, CancellationToken.None).ConfigureAwait(false);
        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        var output = doc.RootElement.GetProperty("hookSpecificOutput");
        output.GetProperty("hookEventName").GetString().Should().Be("SessionStart");
        return output.GetProperty("additionalContext").GetString()!;
    }

    [Fact]
    public void Serves_the_session_start_event() => Create().Event.Should().Be("session-start");

    [Fact]
    public async Task A_valid_project_returns_its_layers()
    {
        using var project = new TempProject();
        project.WriteRules();

        var context = await ContextAsync(project.Root).ConfigureAwait(true);

        context.Should().Contain("Ariadne rules (clean-architecture+shop").And.Contain("Layers:");
    }

    [Theory]
    [InlineData("startup")]
    [InlineData("resume")]
    [InlineData("clear")]
    [InlineData("compact")]
    [InlineData("fork")]
    public async Task Every_source_behaves_the_same(string source)
    {
        using var project = new TempProject();
        project.WriteRules();

        var withSource = await ContextAsync(project.Root, source).ConfigureAwait(true);
        var without = await ContextAsync(project.Root).ConfigureAwait(true);

        withSource.Should().Be(without);
    }

    [Fact]
    public async Task A_project_without_rules_points_at_init()
    {
        using var project = new TempProject();

        (await ContextAsync(project.Root).ConfigureAwait(true)).Should().Contain("ariadne init");
    }

    [Fact]
    public async Task Invalid_rules_are_described_not_thrown()
    {
        using var project = new TempProject();
        project.WriteRules("not: [valid");

        (await ContextAsync(project.Root).ConfigureAwait(true)).Should().Contain("could not load");
    }

    [Fact]
    public async Task Rules_that_extend_an_unknown_builtin_pack_are_not_told_to_init()
    {
        using var project = new TempProject();
        project.WriteRules("schema: 2\nname: shop\nextends:\n  - builtin://no-such-pack\n");

        var context = await ContextAsync(project.Root).ConfigureAwait(true);

        context.Should().Contain("could not load").And.Contain("no-such-pack").And.NotContain("ariadne init");
    }

    [Fact]
    public async Task A_very_long_error_message_stays_within_the_cap()
    {
        using var project = new TempProject();
        project.WriteRules($"schema: 2\nname: shop\nextends:\n  - builtin://{new string('z', 30_000)}\n");

        var context = await ContextAsync(project.Root).ConfigureAwait(true);

        context.Length.Should().BeLessThanOrEqualTo(10_000);
        context.Should().Contain("could not load");
    }

    [Fact]
    public async Task A_stale_cache_warning_comes_first()
    {
        using var project = new TempProject();
        project.WriteRules();
        project.WriteImports("shared", "^1.0.0");
        project.WriteCache("shared", "v2.0.0");

        var context = await ContextAsync(project.Root).ConfigureAwait(true);

        context.Should().StartWith("Warning:").And.Contain("ariadne sync");
    }
}
