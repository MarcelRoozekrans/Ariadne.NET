using System.Reflection;
using FluentAssertions;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Mcp.Resources;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Ariadne.NET.Mcp.Tests.Resources;

public class PackResourcesTests
{
    private readonly BuiltinPackRegistry _registry;

    public PackResourcesTests()
    {
        _registry = new BuiltinPackRegistry(new YamlRulePackLoader());
    }

    [Fact]
    public void GetPackContent_IsExposedUnderAriadneUriScheme()
    {
        var attribute = typeof(PackResources)
            .GetMethod(nameof(PackResources.GetPackContent))!
            .GetCustomAttribute<McpServerResourceAttribute>()!;

        attribute.UriTemplate.Should().Be("ariadne://packs/{name}");
    }

    [Fact]
    public void GetPackContent_CleanArchitecture_ShouldReturnYaml()
    {
        var result = PackResources.GetPackContent(_registry, "clean-architecture");

        result.Should().Contain("name: clean-architecture");
        result.Should().Contain("layers:");
        result.Should().Contain("componentRoles:");
    }

    [Fact]
    public void GetPackContent_UnknownPack_ShouldThrowResourceNotFound()
    {
        var act = () => PackResources.GetPackContent(_registry, "nonexistent");

        var exception = act.Should().Throw<McpProtocolException>().Which;
        exception.ErrorCode.Should().Be(McpErrorCode.ResourceNotFound);
        exception.Message.Should().StartWith("Built-in pack 'nonexistent' not found.\nAvailable packs: ");
        exception.Message.Should().Contain("clean-architecture");
    }
}
