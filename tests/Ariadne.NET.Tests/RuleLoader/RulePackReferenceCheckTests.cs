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
            Layers = layers ?? [Layer("Domain")],
            ComponentRoles = roles ?? [],
        };

    private static LayerDefinition Layer(string name, params string[] allowed) =>
        new() { Name = name, Id = "L-" + name, Because = name + ".", AllowedDependencies = [.. allowed] };

    [Fact]
    public void Consistent_pack_passes()
    {
        var pack = Pack(
            [Layer("Domain"), Layer("App", "domain")],
            [
                new ComponentRoleDefinition { Name = "Entity", Layer = "Domain" },
                new ComponentRoleDefinition { Name = "Service", Layer = "APP", RequiresInterface = true, InterfaceRole = "entity" },
            ]);

        Resolve(pack).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Unknown_allowed_dependency_is_invalid_and_named()
    {
        var pack = Pack([Layer("App", "Ghost")]);

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
        var pack = Pack([Layer("Domain"), new LayerDefinition { Name = "DOMAIN", Id = "L-DOMAIN-2", Because = "x" }]);

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
            [Layer("App", "X", "Y")]);

        Resolve(pack).Error.Detail!.Split('\n').Should().HaveCount(2);
    }

    [Fact]
    public void DuplicateIdAcrossLayerAndRule_Fails()
    {
        var pack = new RulePack
        {
            Name = "p",
            Layers = [new LayerDefinition { Name = "Domain", Id = "X-1" }],
            Rules = [Rule("X-1", ["Domain"], ["Domain"])],
        };

        Resolve(pack).Error.Detail.Should().Be("duplicate id 'X-1'");
    }

    [Fact]
    public void EmptyIds_AreEachReported_NotAsDuplicates()
    {
        var pack = new RulePack
        {
            Name = "p",
            Layers = [new LayerDefinition { Name = "Domain" }, new LayerDefinition { Name = "App" }],
            Rules = [Rule("", ["Domain"], ["App"])],
        };

        Resolve(pack).Error.Detail!.Split('\n').Should().Equal(
            "Layers[0].Id: Id must not be empty.",
            "Layers[1].Id: Id must not be empty.",
            "Rules[0].Id: Id must not be empty.");
    }

    [Fact]
    public void RuleUnknownLayer_Fails()
    {
        var pack = new RulePack
        {
            Name = "p",
            Layers = [new LayerDefinition { Name = "Domain", Id = "L-1" }],
            Rules = [Rule("R-1", ["domain", "Ghost"], ["Nope"])],
        };

        Resolve(pack).Error.Detail!.Split('\n').Should().Equal(
            "rule 'R-1' refers to unknown layer 'Ghost'",
            "rule 'R-1' refers to unknown layer 'Nope'");
    }

    [Fact]
    public void RoleOnlyPack_LoadsAlone()
    {
        var pack = new RulePack
        {
            Name = "cqrs",
            ComponentRoles = [new ComponentRoleDefinition { Name = "Command", Layer = "Application" }],
        };

        Resolve(pack).IsSuccess.Should().BeTrue();
    }

    private static RuleDefinition Rule(string id, string[] fromLayers, string[] toLayers) => new()
    {
        Id = id,
        Because = "because",
        Kind = RuleKind.Forbid,
        From = new TargetSelector { Layers = fromLayers },
        To = new TargetSelector { Layers = toLayers },
    };
}
