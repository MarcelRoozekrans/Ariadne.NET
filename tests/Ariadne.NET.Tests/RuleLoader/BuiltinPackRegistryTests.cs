using Ariadne.NET.Abstractions;
using FluentAssertions;
using Ariadne.NET.RuleLoader;

namespace Ariadne.NET.Tests.RuleLoader;

public class BuiltinPackRegistryTests
{
    [Fact]
    public void GetAvailablePacks_ShouldReturnAllBuiltinPacks()
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var packs = registry.GetAvailablePackNames();

        packs.Should().Contain("clean-architecture");
        packs.Should().Contain("hexagonal-architecture");
        packs.Should().Contain("cqrs");
        packs.Should().Contain("ddd-tactical");
        packs.Should().Contain("event-driven");
        packs.Should().Contain("vertical-slice");
        packs.Should().Contain("modular-monolith");
        packs.Should().Contain("onion-architecture");
        packs.Should().Contain("layered");
        packs.Should().Contain("microservice");
        packs.Should().HaveCount(10);
    }

    [Fact]
    public void GetPack_ShouldReturnCleanArchitecturePack()
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var pack = registry.GetPack("clean-architecture").Value;

        pack.Should().NotBeNull();
        pack.Layers.Should().HaveCount(4);
        pack.ComponentRoles.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("ddd-tactical")]
    [InlineData("event-driven")]
    [InlineData("vertical-slice")]
    [InlineData("modular-monolith")]
    [InlineData("onion-architecture")]
    [InlineData("layered")]
    [InlineData("microservice")]
    public void GetPack_ShouldReturnNewPack(string packName)
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var pack = registry.GetPack(packName).Value;

        pack.Should().NotBeNull();
        pack.Name.Should().Be(packName);
        pack.ComponentRoles.Should().NotBeEmpty();
    }

    [Fact]
    public void GetPack_UnknownName_ShouldReturnNotFound()
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var result = registry.GetPack("nonexistent");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
        result.Error.Message.Should().Be("Built-in pack 'nonexistent' not found.");
        result.Error.Detail.Should().Contain("clean-architecture");
        result.Error.Detail.Should().StartWith("Available packs: ");
    }

    [Fact]
    public void GetPack_UnknownNameTwice_ShouldReturnNotFoundBothTimes()
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());

        registry.GetPack("nonexistent").Error.Code.Should().Be(AriadneErrorCode.NotFound);
        registry.GetPack("nonexistent").Error.Code.Should().Be(AriadneErrorCode.NotFound);
    }

    [Fact]
    public void GetPack_DifferentCase_ShouldReturnPack()
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var result = registry.GetPack("Clean-Architecture");

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("clean-architecture");
    }
}
