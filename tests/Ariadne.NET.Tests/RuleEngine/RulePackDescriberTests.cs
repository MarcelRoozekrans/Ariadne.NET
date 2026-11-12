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
    public void Describes_slices_rule()
    {
        var rule = new RuleDefinition
        {
            Id = "R-SLICES",
            Because = "Modules stay independent.",
            Kind = RuleKind.Slices,
            From = new TargetSelector(),
            Slices = new SliceDefinition
            {
                Matching = ["Shop.(*).**", "Lib.(**).Core"],
                Independent = true,
                Allow = new TargetSelector { Namespaces = ["Shop.*.Public.**"] },
            },
        };

        RulePackDescriber.DescribeRule(rule).Should().Equal(
            "R-SLICES slices matching Shop.(*).**, Lib.(**).Core -> independent; allow namespaces Shop.*.Public.**",
            "Modules stay independent.");
    }

    [Fact]
    public void Describes_free_of_cycles_slices_rule_without_allow()
    {
        var rule = new RuleDefinition
        {
            Id = "R-CYCLES",
            Because = "No cycles.",
            Kind = RuleKind.Slices,
            From = new TargetSelector(),
            Slices = new SliceDefinition { Matching = ["Shop.(*)"], FreeOfCycles = true },
        };

        RulePackDescriber.DescribeRule(rule).Should().Equal("R-CYCLES slices matching Shop.(*) -> freeOfCycles", "No cycles.");
    }

    [Fact]
    public void DescribeRule_BannedPackages_PrintsThePackagesAsTarget()
    {
        var rule = new RuleDefinition
        {
            Id = "R-PACKAGES",
            Because = "No second JSON library.",
            Kind = RuleKind.BannedPackages,
            From = new TargetSelector { Assemblies = ["Shop.Domain"] },
            Packages = ["Newtonsoft.Json", "FluentValidation"],
        };

        RulePackDescriber.DescribeRule(rule).Should().Equal(
            "R-PACKAGES bannedPackages assemblies Shop.Domain -> packages Newtonsoft.Json, FluentValidation",
            "No second JSON library.");
    }

    [Fact]
    public void DescribeRule_UniqueNames_PrintsTheAgainstSelectorAsTarget()
    {
        var rule = new RuleDefinition
        {
            Id = "R-NAMES",
            Because = "Web keeps no copy of an application DTO.",
            Kind = RuleKind.UniqueNames,
            From = new TargetSelector { Layers = ["Web"] },
            Against = new TargetSelector { Namespaces = ["Shop.Application.**.Dtos"] },
        };

        RulePackDescriber.DescribeRule(rule).Should().Equal(
            "R-NAMES uniqueNames layers Web -> against namespaces Shop.Application.**.Dtos",
            "Web keeps no copy of an application DTO.");
    }

    [Fact]
    public void DescribeRule_MembersSelector_ListsMembersLast()
    {
        var rule = new RuleDefinition
        {
            Id = "R-MEMBERS",
            Because = "Domain does not write to the console.",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Layers = ["Domain"] },
            To = new TargetSelector { Members = ["System.Console.*"] },
        };

        RulePackDescriber.DescribeRule(rule).Should().Equal(
            "R-MEMBERS forbid layers Domain -> members System.Console.*",
            "Domain does not write to the console.");
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
    public void Describe_SelectorWithTraits_NamesEachTraitAfterTheOtherKinds()
    {
        var selector = new TargetSelector
        {
            Namespaces = ["Shop.Domain.**"],
            Names = ["*Service"],
            Kinds = [CSharpTypeKind.Class, CSharpTypeKind.Enum, CSharpTypeKind.RecordStruct],
            Accessibility = ["public", "protected internal"],
            AssignableTo = ["**.Base"],
            Sealed = true,
            Abstract = false,
            Static = false,
            Nested = true,
        };

        RulePackDescriber.Describe(selector).Should().Be(
            "namespaces Shop.Domain.**; names *Service; kinds class, enum, recordStruct; accessibility public, protected internal; "
            + "assignableTo **.Base; sealed true; abstract false; static false; nested true");
    }

    [Fact]
    public void Describe_TraitOnlySelector_IsNotNoSelectors()
    {
        RulePackDescriber.Describe(new TargetSelector { Sealed = true }).Should().Be("sealed true");
    }

    [Fact]
    public void Describe_RequirementWithIsAndIsNot_ListsThemAfterImplements()
    {
        var requirement = new RuleRequirement
        {
            Kind = CSharpTypeKind.Class,
            Implements = new TargetSelector { Interfaces = ["**.IDetector"] },
            Is = new TargetSelector { Sealed = true },
            IsNot = new TargetSelector { Names = ["*Impl", "*Helper"] },
        };

        RulePackDescriber.Describe(requirement).Should().Be(
            "kind class, implements interfaces **.IDetector, is sealed true, isNot names *Impl, *Helper");
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

    [Fact]
    public void Describes_requireTarget()
    {
        RuleDefinition Rule(bool allowEmpty, bool requireTarget) => new()
        {
            Id = "R-TARGET",
            Because = "The seam must exist.",
            Kind = RuleKind.AllowOnly,
            From = new TargetSelector { Layers = ["Agents"] },
            To = new TargetSelector { Types = ["Lib.ISubagentRunner"] },
            AllowEmpty = allowEmpty,
            RequireTarget = requireTarget,
        };

        RulePackDescriber.DescribeRule(Rule(allowEmpty: false, requireTarget: true)).Should().Equal(
            "R-TARGET allowOnly layers Agents -> types Lib.ISubagentRunner",
            "requireTarget",
            "The seam must exist.");
        RulePackDescriber.DescribeRule(Rule(allowEmpty: true, requireTarget: true)).Should().Equal(
            "R-TARGET allowOnly layers Agents -> types Lib.ISubagentRunner",
            "allowEmpty",
            "requireTarget",
            "The seam must exist.");
        RulePackDescriber.DescribeRule(Rule(allowEmpty: false, requireTarget: false)).Should().Equal(
            "R-TARGET allowOnly layers Agents -> types Lib.ISubagentRunner",
            "The seam must exist.");
    }

    [Fact]
    public void Describer_shows_allowEmpty_only_when_true()
    {
        RuleDefinition Rule(bool allowEmpty) => new()
        {
            Id = "R-EMPTY",
            Because = "Reserved for later.",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Layers = ["Domain"] },
            To = new TargetSelector { Layers = ["Infrastructure"] },
            AllowEmpty = allowEmpty,
        };

        RulePackDescriber.DescribeRule(Rule(true)).Should().Equal(
            "R-EMPTY forbid layers Domain -> layers Infrastructure",
            "allowEmpty",
            "Reserved for later.");
        RulePackDescriber.DescribeRule(Rule(false)).Should().Equal(
            "R-EMPTY forbid layers Domain -> layers Infrastructure",
            "Reserved for later.");
    }
}
