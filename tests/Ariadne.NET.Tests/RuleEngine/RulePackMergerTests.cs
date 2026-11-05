using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
using FluentAssertions;

namespace Ariadne.NET.Tests.RuleEngine;

public class RulePackMergerTests
{
    private readonly RulePackMerger _merger = new();

    private static LayerDefinition Layer(string name, params string[] namespaces) => new()
    {
        Name = name,
        Id = $"L-{name}",
        Because = $"{name} because",
        Namespaces = namespaces,
        AllowedDependencies = ["Domain"],
    };

    private static RuleDefinition Rule(string id, bool locked = false, params RuleException[] exceptions) => new()
    {
        Id = id,
        Because = "because",
        Kind = RuleKind.Forbid,
        From = new TargetSelector { Layers = ["A"] },
        To = new TargetSelector { Layers = ["B"] },
        Exceptions = exceptions,
        Locked = locked,
    };

    private static RuleException Exception(string because) => new() { Because = because, Types = ["X.Y"] };

    private static string[] Problems(ZeroAlloc.Results.Result<RulePack, AriadneError> result) =>
        result.Error.Detail!.Split('\n');

    [Fact]
    public void TwoParentsSameLayer_Fails()
    {
        var result = _merger.Merge(
        [
            new RulePack { Name = "a", Layers = [Layer("Domain", "**.Domain.**")] },
            new RulePack { Name = "b", Layers = [Layer("Domain", "**.Core.**")] },
        ]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Rule pack 'a+b' is invalid.");
        Problems(result).Should().Equal("packs 'a' and 'b' both define layer 'Domain'");
    }

    [Fact]
    public void TwoParentsSameRule_Fails()
    {
        var result = _merger.Merge(
        [
            new RulePack { Name = "a", Rules = [Rule("R1")] },
            new RulePack { Name = "b", Rules = [Rule("R1")] },
        ]);

        Problems(result).Should().Equal("packs 'a' and 'b' both define rule 'R1'");
    }

    [Fact]
    public void ChildRedefinesInheritedLayer_Fails()
    {
        var result = _merger.Merge(
        [
            new RulePack { Name = "parent", Layers = [Layer("Domain", "**.Domain.**")] },
            new RulePack { Name = "child", Layers = [Layer("Domain", "**.Core.**")] },
        ]);

        Problems(result).Should().Equal("packs 'parent' and 'child' both define layer 'Domain'");
    }

    [Fact]
    public void Retarget_ReplacesSelectorsKeepsDependenciesAndBecause()
    {
        var parentLayer = new LayerDefinition
        {
            Name = "Domain",
            Id = "L-Domain",
            Because = "Domain because",
            Namespaces = ["**.Domain.**"],
            Assemblies = ["Old.Domain"],
            AllowedDependencies = ["Domain"],
        };

        var result = _merger.Merge(
        [
            new RulePack { Name = "parent", Layers = [parentLayer] },
            new RulePack
            {
                Name = "child",
                LayerRetargets = [new LayerRetarget { Name = "domain", Namespaces = ["Shop.Core.**"] }],
            },
        ]);

        result.IsSuccess.Should().BeTrue();
        var layer = result.Value.Layers.Should().ContainSingle().Subject;
        layer.Name.Should().Be("Domain");
        layer.Namespaces.Should().Equal("Shop.Core.**");
        layer.Assemblies.Should().BeEmpty("a retarget replaces namespaces and assemblies together");
        layer.AllowedDependencies.Should().Equal("Domain");
        layer.Because.Should().Be("Domain because");
        layer.Id.Should().Be("L-Domain");
        result.Value.LayerRetargets.Should().BeEmpty();
        result.Value.RuleAmendments.Should().BeEmpty();
        result.Value.Name.Should().Be("parent+child");
        result.Value.Schema.Should().Be(2);
    }

    [Fact]
    public void RetargetUnknownLayer_Fails()
    {
        var result = _merger.Merge(
        [
            new RulePack { Name = "parent", Layers = [Layer("Domain", "**.Domain.**")] },
            new RulePack { Name = "child", LayerRetargets = [new LayerRetarget { Name = "Ghost", Namespaces = ["X"] }] },
        ]);

        Problems(result).Should().Equal("pack 'child' retargets layer 'Ghost', which no extended pack defines");
    }

    [Fact]
    public void Amendment_AppendsExceptions()
    {
        var result = _merger.Merge(
        [
            new RulePack { Name = "parent", Rules = [Rule("R1", false, Exception("one"))] },
            new RulePack { Name = "child", RuleAmendments = [new RuleAmendment { Id = "R1", Exceptions = [Exception("two")] }] },
        ]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rules.Should().ContainSingle().Which.Exceptions.Select(e => e.Because).Should().Equal("one", "two");
    }

    [Fact]
    public void AmendmentOfLockedRule_Fails()
    {
        var result = _merger.Merge(
        [
            new RulePack { Name = "org", Rules = [Rule("R1", true)] },
            new RulePack { Name = "team", RuleAmendments = [new RuleAmendment { Id = "R1", Exceptions = [Exception("two")] }] },
        ]);

        Problems(result).Should().Equal("rule 'R1' from pack 'org' is locked; pack 'team' cannot add exceptions");
    }

    [Fact]
    public void AmendmentUnknownRule_Fails()
    {
        var result = _merger.Merge(
        [
            new RulePack { Name = "parent", Rules = [Rule("R1")] },
            new RulePack { Name = "child", RuleAmendments = [new RuleAmendment { Id = "R9", Exceptions = [Exception("two")] }] },
        ]);

        Problems(result).Should().Equal("pack 'child' amends rule 'R9', which no extended pack defines");
    }

    [Fact]
    public void RoleOnlyAddOn_ComposesWithLayerPack()
    {
        var result = _merger.Merge(
        [
            new RulePack
            {
                Name = "base",
                Layers = [Layer("Domain", "**.Domain.**")],
                ComponentRoles = [new ComponentRoleDefinition { Name = "Entity", Layer = "Domain", TypeKind = CSharpTypeKind.Class }],
                NamingConventions = new NamingConventions { Namespace = "old" },
            },
            new RulePack
            {
                Name = "cqrs",
                ComponentRoles =
                [
                    new ComponentRoleDefinition { Name = "Command", Layer = "Domain", TypeKind = CSharpTypeKind.Record },
                    new ComponentRoleDefinition { Name = "entity", Layer = "Domain", TypeKind = CSharpTypeKind.Record },
                ],
                NamingConventions = new NamingConventions { Namespace = "new" },
            },
        ]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Layers.Should().HaveCount(1);
        result.Value.ComponentRoles.Should().HaveCount(2);
        result.Value.ComponentRoles.First(r => string.Equals(r.Name, "Entity", StringComparison.OrdinalIgnoreCase))
            .TypeKind.Should().Be(CSharpTypeKind.Record);
        result.Value.NamingConventions.Namespace.Should().Be("new");
    }

    [Fact]
    public void ThreeProblems_AllReported()
    {
        var result = _merger.Merge(
        [
            new RulePack { Name = "a", Layers = [Layer("Domain", "x")], Rules = [Rule("R1")] },
            new RulePack
            {
                Name = "b",
                Layers = [Layer("Domain", "y")],
                Rules = [Rule("R1")],
                LayerRetargets = [new LayerRetarget { Name = "Ghost", Namespaces = ["X"] }],
            },
        ]);

        Problems(result).Should().Equal(
            "pack 'b' retargets layer 'Ghost', which no extended pack defines",
            "packs 'a' and 'b' both define layer 'Domain'",
            "packs 'a' and 'b' both define rule 'R1'");
    }

    [Fact]
    public void LayerNamesDifferingInCase_Conflict()
    {
        var result = _merger.Merge(
        [
            new RulePack { Name = "a", Layers = [Layer("Domain", "x")] },
            new RulePack { Name = "b", Layers = [Layer("domain", "y")] },
        ]);

        Problems(result).Should().Equal("packs 'a' and 'b' both define layer 'domain'");
    }

    [Fact]
    public void RuleIdsDifferingInCase_Conflict()
    {
        var result = _merger.Merge(
        [
            new RulePack { Name = "a", Rules = [Rule("r1")] },
            new RulePack { Name = "b", Rules = [Rule("R1")] },
        ]);

        Problems(result).Should().Equal("packs 'a' and 'b' both define rule 'R1'");
    }

    [Fact]
    public void RetargetOfLayerInSamePack_Fails()
    {
        var result = _merger.Merge(
        [
            new RulePack
            {
                Name = "only",
                Layers = [Layer("Domain", "x")],
                LayerRetargets = [new LayerRetarget { Name = "Domain", Namespaces = ["y"] }],
            },
        ]);

        Problems(result).Should().Equal("pack 'only' retargets layer 'Domain', which no extended pack defines");
    }

    [Fact]
    public void RetargetOfLayerInLaterPack_Fails()
    {
        var result = _merger.Merge(
        [
            new RulePack { Name = "first", LayerRetargets = [new LayerRetarget { Name = "Domain", Namespaces = ["y"] }] },
            new RulePack { Name = "second", Layers = [Layer("Domain", "x")] },
        ]);

        Problems(result).Should().Equal("pack 'first' retargets layer 'Domain', which no extended pack defines");
    }

    [Fact]
    public void AmendmentOfRuleInSamePack_Fails()
    {
        var result = _merger.Merge(
        [
            new RulePack
            {
                Name = "only",
                Rules = [Rule("R1")],
                RuleAmendments = [new RuleAmendment { Id = "R1", Exceptions = [Exception("two")] }],
            },
        ]);

        Problems(result).Should().Equal("pack 'only' amends rule 'R1', which no extended pack defines");
    }

    [Fact]
    public void AmendmentOfRuleInLaterPack_Fails()
    {
        var result = _merger.Merge(
        [
            new RulePack { Name = "first", RuleAmendments = [new RuleAmendment { Id = "R1", Exceptions = [Exception("two")] }] },
            new RulePack { Name = "second", Rules = [Rule("R1")] },
        ]);

        Problems(result).Should().Equal("pack 'first' amends rule 'R1', which no extended pack defines");
    }

    [Fact]
    public void EmptyList_MergesToEmptyPack()
    {
        var result = _merger.Merge([]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().BeEmpty();
    }

    [Fact]
    public void NullList_Throws()
    {
        var act = () => _merger.Merge(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
