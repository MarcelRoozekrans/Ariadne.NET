using System.ComponentModel;
using System.Reflection;
using Ariadne.NET.Abstractions;
using FluentAssertions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Mcp.Tools;
using ZeroAlloc.Results;

namespace Ariadne.NET.Mcp.Tests.Tools;

public class RuleToolsTests
{
    private readonly RuleResolutionHelper _helper;

    public RuleToolsTests()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        _helper = new RuleResolutionHelper(
            registry,
            new RulePackResolver(registry, loader, new RulePackMerger()),
            loader);
    }

    [Fact]
    public void GetComponentRoles_ShouldReturnRolesForTemplate()
    {
        var result = RuleTools.GetComponentRoles(_helper, "clean-architecture");

        result.Should().Contain(r => r.Name == "Entity");
        result.Should().Contain(r => r.Name == "Repository");
    }

    [Fact]
    public void GetComponentRoles_CompositeTemplate_ShouldMergeRolesFromEachPack()
    {
        var result = RuleTools.GetComponentRoles(_helper, "clean-architecture+cqrs");

        result.Should().Contain(r => r.Name == "Entity");
        result.Should().Contain(r => r.Name == "Command");
    }

    [Fact]
    public void GetRules_ReturnsTheResolvedRules()
    {
        var rule = new RuleDefinition
        {
            Id = "SHOP-NO-EF-IN-DOMAIN",
            Because = "Persistence belongs to Infrastructure.",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Layers = ["Domain"] },
            To = new TargetSelector { Namespaces = ["Microsoft.EntityFrameworkCore.**"] },
        };
        var pack = new RulePack { Name = "shop", Rules = [rule] };

        var result = RuleTools.GetRules(new FixedHelper(pack), template: null);

        result.Should().BeSameAs(pack.Rules);
    }

    [Fact]
    public void GetRules_ReturnsRequireRulesWithTheirRequirement()
    {
        var rule = new RuleDefinition
        {
            Id = "SHOP-REPOS-IN-INFRA",
            Because = "Repositories live in Infrastructure.",
            Kind = RuleKind.Require,
            From = new TargetSelector { Interfaces = ["**.I*Repository"] },
            Require = new RuleRequirement { Layer = "Infrastructure", Kind = CSharpTypeKind.Class, Accessibility = "internal" },
        };
        var pack = new RulePack { Name = "shop", Rules = [rule] };

        var result = RuleTools.GetRules(new FixedHelper(pack), template: null);

        var returned = result.Should().ContainSingle().Subject;
        returned.Kind.Should().Be(RuleKind.Require);
        returned.Require.Should().NotBeNull();
        returned.Require!.Layer.Should().Be("Infrastructure");
        returned.Require.Kind.Should().Be(CSharpTypeKind.Class);
        returned.Require.Accessibility.Should().Be("internal");
        typeof(RuleTools).GetMethod(nameof(RuleTools.GetRules))!
            .GetCustomAttribute<DescriptionAttribute>()!.Description.Should().Contain("require");
    }

    [Fact]
    public void GetRules_ListsNewKinds()
    {
        var banned = new RuleDefinition
        {
            Id = "SHOP-NO-NEWTONSOFT",
            Because = "JSON goes through System.Text.Json.",
            Kind = RuleKind.BannedPackages,
            From = new TargetSelector { Assemblies = ["Shop.*"] },
            Packages = ["Newtonsoft.Json"],
        };
        var unique = new RuleDefinition
        {
            Id = "SHOP-UNIQUE",
            Because = "Web keeps its own names.",
            Kind = RuleKind.UniqueNames,
            From = new TargetSelector { Layers = ["Web"] },
            Against = new TargetSelector { Layers = ["Domain"] },
        };
        var members = new RuleDefinition
        {
            Id = "SHOP-NO-CONSOLE",
            Because = "No console in the domain.",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Layers = ["Domain"] },
            To = new TargetSelector { Members = ["System.Console.*"] },
        };
        var pack = new RulePack { Name = "shop", Rules = [banned, unique, members] };

        var result = RuleTools.GetRules(new FixedHelper(pack), template: null);

        result.Select(r => r.Kind).Should().Equal(RuleKind.BannedPackages, RuleKind.UniqueNames, RuleKind.Forbid);
        result[0].Packages.Should().Equal("Newtonsoft.Json");
        result[1].Against.Layers.Should().Equal("Domain");
        result[2].To.Members.Should().Equal("System.Console.*");
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        json.Should().Contain("\"Packages\":[\"Newtonsoft.Json\"]").And.Contain("\"Members\":[\"System.Console.*\"]");
    }

    [Fact]
    public void Describer_shows_allowEmpty_only_when_true()
    {
        RuleDefinition Rule(string id, bool allowEmpty) => new()
        {
            Id = id,
            Because = "Because.",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Layers = ["Domain"] },
            AllowEmpty = allowEmpty,
        };
        var pack = new RulePack
        {
            Name = "shop",
            Rules = [Rule("R-1", true), Rule("R-2", false)],
            ComponentRoles =
            [
                new ComponentRoleDefinition { Name = "Reserved", Layer = "Domain", AllowEmpty = true },
                new ComponentRoleDefinition { Name = "Active", Layer = "Domain" },
            ],
        };
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        var rules = System.Text.Json.JsonSerializer.SerializeToElement(RuleTools.GetRules(new FixedHelper(pack), template: null), options);
        var roles = System.Text.Json.JsonSerializer.SerializeToElement(RuleTools.GetComponentRoles(new FixedHelper(pack), template: null), options);

        rules[0].GetProperty("allowEmpty").GetBoolean().Should().BeTrue();
        rules[1].TryGetProperty("allowEmpty", out _).Should().BeFalse();
        roles[0].GetProperty("allowEmpty").GetBoolean().Should().BeTrue();
        roles[1].TryGetProperty("allowEmpty", out _).Should().BeFalse();
    }

    [Fact]
    public void GetRules_BuiltinWithoutRules_ReturnsEmpty()
    {
        RuleTools.GetRules(_helper, "clean-architecture").Should().BeEmpty();
    }

    [Fact]
    public void GetRules_IsDescribedAsEveryRuleKind()
    {
        var attribute = typeof(RuleTools).GetMethod(nameof(RuleTools.GetRules))!
            .GetCustomAttribute<DescriptionAttribute>()!;

        attribute.Description.Should().Be(
            "Get the forbid, allowOnly, require, bannedPackages and uniqueNames rules of the resolved pack, each with its id, targets, requirement, packages, against selector, exceptions and because. A forbid or allowOnly target may select members.");
    }

    private sealed class FixedHelper(RulePack pack) : IRuleResolutionHelper
    {
        public Result<RulePack, AriadneError> Resolve(string? template) => Result<RulePack, AriadneError>.Success(pack);
    }
}
