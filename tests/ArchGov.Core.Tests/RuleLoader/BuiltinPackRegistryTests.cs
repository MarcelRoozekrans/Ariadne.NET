using FluentAssertions;
using ArchGov.Core.RuleLoader;

namespace ArchGov.Core.Tests.RuleLoader;

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
        packs.Should().HaveCount(7);
    }

    [Fact]
    public void GetPack_ShouldReturnCleanArchitecturePack()
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var pack = registry.GetPack("clean-architecture");

        pack.Should().NotBeNull();
        pack.Layers.Should().HaveCount(4);
        pack.ComponentRoles.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("ddd-tactical")]
    [InlineData("event-driven")]
    [InlineData("vertical-slice")]
    [InlineData("modular-monolith")]
    public void GetPack_ShouldReturnNewPack(string packName)
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var pack = registry.GetPack(packName);

        pack.Should().NotBeNull();
        pack.Name.Should().Be(packName);
        pack.ComponentRoles.Should().NotBeEmpty();
    }

    [Fact]
    public void GetPack_UnknownName_ShouldThrow()
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var act = () => registry.GetPack("nonexistent");

        act.Should().Throw<KeyNotFoundException>();
    }
}
