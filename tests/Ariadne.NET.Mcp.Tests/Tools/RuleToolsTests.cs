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
    public void GetRules_BuiltinWithoutRules_ReturnsEmpty()
    {
        RuleTools.GetRules(_helper, "clean-architecture").Should().BeEmpty();
    }

    [Fact]
    public void GetRules_IsDescribedAsTheForbidAllowOnlyAndRequireRules()
    {
        var attribute = typeof(RuleTools).GetMethod(nameof(RuleTools.GetRules))!
            .GetCustomAttribute<DescriptionAttribute>()!;

        attribute.Description.Should().Be(
            "Get the forbid, allowOnly and require rules of the resolved pack, each with its id, targets, requirement, exceptions and because.");
    }

    private sealed class FixedHelper(RulePack pack) : IRuleResolutionHelper
    {
        public Result<RulePack, AriadneError> Resolve(string? template) => Result<RulePack, AriadneError>.Success(pack);
    }
}
