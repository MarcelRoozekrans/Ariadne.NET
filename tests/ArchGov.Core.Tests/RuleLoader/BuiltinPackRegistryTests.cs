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

    [Fact]
    public void GetPack_UnknownName_ShouldThrow()
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var act = () => registry.GetPack("nonexistent");

        act.Should().Throw<KeyNotFoundException>();
    }
}
