using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;
using FluentAssertions;

namespace Ariadne.NET.Tests.RuleLoader;

public class RulePackReferenceCheckTests
{
    // The check is internal to the engine; the resolver runs it on every resolve, including a pack with no extends.
    private static ZeroAlloc.Results.Result<RulePack, AriadneError> Resolve(RulePack pack) =>
        new RulePackResolver(
            new BuiltinPackRegistry(new YamlRulePackLoader()),
            new YamlRulePackLoader(),
            new Ariadne.NET.RuleEngine.RulePackMerger()).Resolve(pack);

    private static RulePack Pack(
        IList<LayerDefinition>? layers = null,
        IList<ComponentRoleDefinition>? roles = null) =>
        new()
        {
            Name = "p",
            Layers = layers ?? [new LayerDefinition { Name = "Domain" }],
            ComponentRoles = roles ?? [],
        };

    [Fact]
    public void Consistent_pack_passes()
    {
        var pack = Pack(
            [new LayerDefinition { Name = "Domain" }, new LayerDefinition { Name = "App", AllowedDependencies = ["domain"] }],
            [
                new ComponentRoleDefinition { Name = "Entity", Layer = "Domain" },
                new ComponentRoleDefinition { Name = "Service", Layer = "APP", RequiresInterface = true, InterfaceRole = "entity" },
            ]);

        Resolve(pack).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Unknown_allowed_dependency_is_invalid_and_named()
    {
        var pack = Pack([new LayerDefinition { Name = "App", AllowedDependencies = ["Ghost"] }]);

        var result = Resolve(pack);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Rule pack 'p' is invalid.");
        result.Error.Detail.Should().Be("Layers[0].AllowedDependencies[0]: refers to unknown layer 'Ghost'.");
    }

    [Fact]
    public void Role_in_an_unknown_layer_is_invalid()
    {
        var pack = Pack(roles: [new ComponentRoleDefinition { Name = "Entity", Layer = "Ghost" }]);

        Resolve(pack).Error.Detail
            .Should().Be("ComponentRoles[0].Layer: refers to unknown layer 'Ghost'.");
    }

    [Fact]
    public void Pack_without_layers_may_name_any_role_layer()
    {
        var pack = Pack(layers: [], roles: [new ComponentRoleDefinition { Name = "Command", Layer = "Application" }]);

        Resolve(pack).IsSuccess.Should().BeTrue("role-only packs such as cqrs declare no layers");
    }

    [Fact]
    public void Unknown_interface_role_is_invalid()
    {
        var pack = Pack(roles: [new ComponentRoleDefinition { Name = "Service", Layer = "Domain", InterfaceRole = "Ghost" }]);

        Resolve(pack).Error.Detail
            .Should().Be("ComponentRoles[0].InterfaceRole: refers to unknown role 'Ghost'.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Absent_interface_role_passes(string? interfaceRole)
    {
        var pack = Pack(roles: [new ComponentRoleDefinition { Name = "Service", Layer = "Domain", InterfaceRole = interfaceRole }]);

        Resolve(pack).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Layer_names_differing_only_in_case_are_duplicates()
    {
        var pack = Pack([new LayerDefinition { Name = "Domain" }, new LayerDefinition { Name = "DOMAIN" }]);

        Resolve(pack).Error.Detail
            .Should().Be("Layers[1].Name: duplicate layer name 'DOMAIN'.");
    }

    [Fact]
    public void Role_names_differing_only_in_case_are_duplicates()
    {
        var pack = Pack(roles:
        [
            new ComponentRoleDefinition { Name = "Entity", Layer = "Domain" },
            new ComponentRoleDefinition { Name = "entity", Layer = "Domain" },
        ]);

        Resolve(pack).Error.Detail
            .Should().Be("ComponentRoles[1].Name: duplicate role name 'entity'.");
    }

    [Fact]
    public void Every_failure_is_listed_on_its_own_line()
    {
        var pack = Pack(
            [new LayerDefinition { Name = "App", AllowedDependencies = ["X", "Y"] }]);

        Resolve(pack).Error.Detail!.Split('\n').Should().HaveCount(2);
    }
}
