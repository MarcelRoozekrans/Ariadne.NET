using FluentAssertions;

namespace Ariadne.NET.Mcp.Tests.Tools;

/// <summary>The report of <c>validate_source</c> as an MCP client receives it.</summary>
[Collection(FixtureSolutionCollection.Name)]
public sealed class ValidateSourceResultTests : IAsyncLifetime
{
    private McpClientHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await McpClientHarness.StartAsync().ConfigureAwait(false);

    public async Task DisposeAsync() => await _harness.DisposeAsync().ConfigureAwait(false);

    [Fact]
    public async Task ValidateSource_BrokenFixture_SerialisesTheViolationKindByName()
    {
        FixtureProjects.EnsureRestored();
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["path"] = FixtureProjects.Path("broken", "Shop.slnx"),
            ["template"] = "clean-architecture",
        };

        var result = await _harness.CallToolAsync("validate_source", arguments);

        result.IsError.Should().NotBe(true);
        var text = McpClientHarness.TextOf(result);
        text.Should().Contain("\"kind\":\"DependencyNotAllowed\"", text);
    }

    [Fact]
    public async Task Validate_reports_a_vacuous_layer_by_kind_name()
    {
        FixtureProjects.EnsureRestored();
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["path"] = FixtureProjects.Path("broken", "Shop.slnx"),
            ["template"] = "clean-architecture",
        };

        var result = await _harness.CallToolAsync("validate_source", arguments);

        result.IsError.Should().NotBe(true);
        var text = McpClientHarness.TextOf(result);
        text.Should().Contain("\"kind\":\"VacuousLayer\"", text);
    }
}
