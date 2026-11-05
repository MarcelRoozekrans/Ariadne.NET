using FluentAssertions;

namespace Ariadne.NET.Mcp.Tests.Tools;

/// <summary>
/// A pack that cannot be resolved, or a solution or project that cannot be loaded, must reach the MCP client as a
/// tool error result, not as an exception or a JSON-RPC error, so the model sees the message and can correct its call.
/// </summary>
[Collection(FixtureSolutionCollection.Name)]
public sealed class ToolErrorResultTests : IAsyncLifetime
{
    private McpClientHarness _harness = null!;

    public static TheoryData<string> PackResolvingTools => new()
    {
        "get_layers",
        "get_component_roles",
        "get_rules",
        "generate_component",
        "generate_workspace",
        "validate_source",
    };

    public async Task InitializeAsync() => _harness = await McpClientHarness.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _harness.DisposeAsync().ConfigureAwait(false);

    [Fact]
    public async Task ToolList_DoesNotContainLintSource()
    {
        var tools = await _harness.Client.ListToolsAsync();

        tools.Select(t => t.Name).Should().Contain("validate_source").And.NotContain("lint_source");
    }

    [Theory]
    [MemberData(nameof(PackResolvingTools))]
    public async Task Tool_UnknownTemplate_ReturnsToolErrorResultWithMessageAndDetail(string toolName)
    {
        // CallToolAsync returning at all shows that no exception and no JSON-RPC error reached the client.
        var result = await _harness.CallToolAsync(toolName, ArgumentsFor(toolName, template: "nope"));

        result.IsError.Should().BeTrue();
        var lines = McpClientHarness.TextOf(result).Split('\n');
        lines[0].Should().Be($"An error occurred invoking '{toolName}': Built-in pack 'nope' not found.");
        lines[1].Should().StartWith("Available packs: ").And.Contain("clean-architecture");
    }

    [Fact]
    public async Task Tool_TemplateWithoutNames_ReturnsToolErrorResult()
    {
        var result = await _harness.CallToolAsync("get_layers", ArgumentsFor("get_layers", template: " + "));

        result.IsError.Should().BeTrue();
        McpClientHarness.TextOf(result).Should().Contain("Template expression ' + ' contains no template names.");
    }

    [Fact]
    public async Task Tool_KnownTemplate_ReturnsSuccessResult()
    {
        var result = await _harness.CallToolAsync("get_layers", ArgumentsFor("get_layers", template: "clean-architecture"));

        result.IsError.Should().NotBe(true);
        McpClientHarness.TextOf(result).Should().Contain("Domain");
    }

    [Fact]
    public async Task ValidateSource_MissingPath_ReturnsToolErrorResultWithMessage()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"ariadne-missing-{Guid.NewGuid():N}", "Nope.slnx");

        var result = await _harness.CallToolAsync("validate_source", PathArguments(missing));

        result.IsError.Should().BeTrue();
        var lines = McpClientHarness.TextOf(result).Split('\n');
        lines[0].Should().Be($"An error occurred invoking 'validate_source': Path '{missing}' does not exist.");
    }

    [Fact]
    public async Task ValidateSource_CompileErrorProject_ReturnsToolErrorResultWithMessageAndDetail()
    {
        FixtureProjects.EnsureRestored();

        var result = await _harness.CallToolAsync("validate_source", PathArguments(FixtureProjects.Path("compile-error", "Broken.csproj")));

        result.IsError.Should().BeTrue();
        var lines = McpClientHarness.TextOf(result).Split('\n');
        lines[0].Should().StartWith("An error occurred invoking 'validate_source': ").And.Contain("'Broken'");
        lines.Length.Should().BeGreaterThan(1);
        lines[1].Should().Contain("Broken.cs:5:").And.Contain("CS0029");
    }

    private static Dictionary<string, object?> PathArguments(string path) =>
        new(StringComparer.Ordinal) { ["path"] = path, ["template"] = "clean-architecture" };

    private static Dictionary<string, object?> ArgumentsFor(string toolName, string template)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal) { ["template"] = template };

        switch (toolName)
        {
            case "generate_component":
                arguments["role"] = "Entity";
                arguments["name"] = "Order";
                arguments["solution"] = "MyApp";
                arguments["feature"] = "Orders";
                break;
            case "generate_workspace":
                arguments["solution"] = "MyApp";
                arguments["features"] = "Orders";
                break;
            case "validate_source":
                arguments["path"] = Path.GetTempPath();
                break;
        }

        return arguments;
    }
}
