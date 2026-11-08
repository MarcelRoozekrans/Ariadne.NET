using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
using FluentAssertions;

namespace Ariadne.NET.Tests.RuleEngine;

public class RulePackDescriberTests
{
    private static RuleException Exception(string because) => new() { Because = because };

    [Fact]
    public void DescribeLayer_WithSelectorsAndDependencies_ListsBoth()
    {
        var layer = new LayerDefinition
        {
            Name = "Mixed",
            Id = "SH-LAYER-MIXED",
            Namespaces = ["**.Mixed.**"],
            Assemblies = ["Shop.Mixed"],
            AllowedDependencies = ["Data", "Core"],
        };

        RulePackDescriber.DescribeLayer(layer).Should().Be(
            "Mixed [SH-LAYER-MIXED]: namespaces **.Mixed.**; assemblies Shop.Mixed; depends on Data, Core");
    }

    [Fact]
    public void DescribeLayer_WithoutDependencies_SaysNothing()
    {
        var layer = new LayerDefinition { Name = "Data", Id = "SH-LAYER-DATA", Assemblies = ["Shop.Data"] };

        RulePackDescriber.DescribeLayer(layer).Should().Be("Data [SH-LAYER-DATA]: assemblies Shop.Data; depends on nothing");
    }

    [Fact]
    public void DescribeRule_Forbid_PrintsKindFromAndTarget()
    {
        var rule = new RuleDefinition
        {
            Id = "R-FORBID",
            Because = "Domain stays pure.",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Layers = ["Domain"] },
            To = new TargetSelector { Layers = ["Infrastructure"] },
        };

        RulePackDescriber.DescribeRule(rule).Should().Equal(
            "R-FORBID forbid layers Domain -> layers Infrastructure",
            "Domain stays pure.");
    }

    [Fact]
    public void DescribeRule_AllowOnly_UsesTheYamlSpelling()
    {
        var rule = new RuleDefinition
        {
            Id = "R-ALLOW",
            Because = "Only the API calls in.",
            Kind = RuleKind.AllowOnly,
            From = new TargetSelector { Namespaces = ["Shop.Api.**"] },
            To = new TargetSelector { Namespaces = ["Shop.Core.**"] },
        };

        RulePackDescriber.DescribeRule(rule).Should().Equal(
            "R-ALLOW allowOnly namespaces Shop.Api.** -> namespaces Shop.Core.**",
            "Only the API calls in.");
    }

    [Fact]
    public void DescribeRule_Require_PrintsTheRequirementAsTarget()
    {
        var rule = new RuleDefinition
        {
            Id = "ARIADNE-RULE-ENGINES-INTERNAL",
            Because = "Engines are not public API.",
            Kind = RuleKind.Require,
            From = new TargetSelector { Types = ["Ariadne.NET.Analysis.*Rules"] },
            Require = new RuleRequirement { Kind = CSharpTypeKind.Class, Accessibility = "internal" },
        };

        RulePackDescriber.DescribeRule(rule).Should().Equal(
            "ARIADNE-RULE-ENGINES-INTERNAL require types Ariadne.NET.Analysis.*Rules -> kind class, internal",
            "Engines are not public API.");
    }

    [Fact]
    public void DescribeRule_WithExceptions_PutsTheCountBeforeTheReason()
    {
        var rule = new RuleDefinition
        {
            Id = "R-EXC",
            Because = "Because.",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Layers = ["A"] },
            To = new TargetSelector { Layers = ["B"] },
            Exceptions = [Exception("one"), Exception("two")],
        };

        RulePackDescriber.DescribeRule(rule).Should().Equal(
            "R-EXC forbid layers A -> layers B",
            "2 exception(s)",
            "Because.");
    }

    [Fact]
    public void Describe_NullRequirement_SaysNoRequirement()
    {
        RulePackDescriber.Describe((RuleRequirement?)null).Should().Be("no requirement");
    }

    [Fact]
    public void Describe_RequirementKind_UsesTheYamlSpelling()
    {
        RulePackDescriber.Describe(new RuleRequirement { Kind = CSharpTypeKind.RecordStruct }).Should().Be("kind recordStruct");
    }

    [Fact]
    public void Describe_SelectorWithNothingSelected_SaysNoSelectors()
    {
        // The loader rejects a layer without selectors, so only the renderer can be pinned directly.
        RulePackDescriber.Describe(new TargetSelector()).Should().Be("no selectors");
    }

    [Fact]
    public void Describe_RequirementWithEveryMember_UsesTheDocumentedOrder()
    {
        var requirement = new RuleRequirement
        {
            Layer = "Domain",
            Kind = CSharpTypeKind.Class,
            Accessibility = "internal",
            Implements = new TargetSelector { Interfaces = ["IDetector"] },
        };

        RulePackDescriber.Describe(requirement).Should().Be("layer Domain, kind class, internal, implements interfaces IDetector");
    }

    [Fact]
    public void DescribeRule_UnknownKind_FallsBackToTheEnumName()
    {
        var rule = new RuleDefinition
        {
            Id = "R-UNKNOWN",
            Because = "Because.",
            Kind = (RuleKind)999,
            From = new TargetSelector { Layers = ["A"] },
            To = new TargetSelector { Layers = ["B"] },
        };

        RulePackDescriber.DescribeRule(rule)[0].Should().Be("R-UNKNOWN 999 layers A -> layers B");
    }
}
