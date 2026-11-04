using FluentAssertions;

namespace Ariadne.NET.Mcp.Tests.Tools;

/// <summary>
/// A pack that cannot be resolved must reach the MCP client as a tool error result, not as an exception
/// or a JSON-RPC error, so the model sees the message and can correct its call.
/// </summary>
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
        "lint_source",
    };

    public async Task InitializeAsync() => _harness = await McpClientHarness.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _harness.DisposeAsync().ConfigureAwait(false);

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
            case "lint_source":
                arguments["sourceDirectory"] = Path.GetTempPath();
                break;
        }

        return arguments;
    }
}
