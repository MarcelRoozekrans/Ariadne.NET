using FluentAssertions;
using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Tests.Abstractions;

public class RulePackTests
{
    [Fact]
    public void RulePack_ShouldHoldLayersAndRoles()
    {
        var pack = new RulePack
        {
            Name = "clean-architecture",
            Version = "1.0.0",
            Author = "platform-team",
            Layers =
            [
                new LayerDefinition { Name = "Domain", Description = "Entities", AllowedDependencies = [] },
                new LayerDefinition { Name = "Application", Description = "Use cases", AllowedDependencies = ["Domain"] }
            ],
            ComponentRoles =
            [
                new ComponentRoleDefinition
                {
                    Name = "Entity",
                    Layer = "Domain",
                    TypeKind = CSharpTypeKind.Class,
                    RequiresInterface = false
                }
            ],
            NamingConventions = new NamingConventions
            {
                Namespace = "{SolutionName}.{Feature}.{Layer}",
                FilePath = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs"
            }
        };

        pack.Name.Should().Be("clean-architecture");
        pack.Layers.Should().HaveCount(2);
        pack.Layers[1].AllowedDependencies.Should().Contain("Domain");
        pack.ComponentRoles.Should().HaveCount(1);
        pack.ComponentRoles[0].TypeKind.Should().Be(CSharpTypeKind.Class);
    }

    [Fact]
    public void NewPack_DefaultsToSchema2_AndEmptyV2Lists()
    {
        var pack = new RulePack { Name = "p" };

        pack.Schema.Should().Be(2);
        pack.Rules.Should().BeEmpty();
        pack.LayerRetargets.Should().BeEmpty();
        pack.RuleAmendments.Should().BeEmpty();
    }

    [Fact]
    public void RuleDefinition_WithoutBecause_FailsValidation()
    {
        var rule = new RuleDefinition { Id = "r1", Because = "", Kind = RuleKind.Forbid, From = new TargetSelector(), To = new TargetSelector() };

        RuleValidator().Validate(rule).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RuleException_WithoutBecause_FailsValidation()
    {
        var exception = new RuleException { Because = "" };

        new RuleExceptionValidator().Validate(exception).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Rules_WithNullEntry_FailsValidation()
    {
        var pack = new RulePack { Name = "p", Rules = [null!] };

        PackValidator().Validate(pack).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RuleDefinition_WithInvalidKind_FailsValidation()
    {
        var rule = new RuleDefinition { Id = "r1", Because = "why", Kind = (RuleKind)99, From = new TargetSelector(), To = new TargetSelector() };

        RuleValidator().Validate(rule).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RuleAmendment_WithoutId_FailsValidation()
    {
        var amendment = new RuleAmendment { Id = "" };

        new RuleAmendmentValidator(new RuleExceptionValidator()).Validate(amendment).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "Kept apart.")]
    [InlineData("L-DOMAIN", "")]
    public void Layer_WithoutIdOrBecause_FailsValidation(string id, string because)
    {
        var layer = new LayerDefinition { Name = "Domain", Id = id, Because = because, Namespaces = ["**.Domain.**"] };

        new LayerDefinitionValidator().Validate(layer).IsValid.Should().BeFalse();
    }

    [Fact]
    public void LayerRetarget_WithoutName_FailsValidation()
    {
        var retarget = new LayerRetarget { Name = "" };

        new LayerRetargetValidator().Validate(retarget).IsValid.Should().BeFalse();
    }

    [Fact]
    public void TargetSelector_DefaultsNewListsToEmpty()
    {
        var selector = new TargetSelector();

        selector.BaseTypes.Should().BeEmpty();
        selector.Interfaces.Should().BeEmpty();
        selector.Attributes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("BaseTypes")]
    [InlineData("Interfaces")]
    [InlineData("Attributes")]
    public void TargetSelector_WithNullEntryInNewList_FailsValidation(string list)
    {
        var selector = list switch
        {
            "BaseTypes" => new TargetSelector { BaseTypes = ["Base", null!] },
            "Interfaces" => new TargetSelector { Interfaces = ["IFoo", null!] },
            _ => new TargetSelector { Attributes = ["Foo", null!] },
        };

        new TargetSelectorValidator().Validate(selector).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RuleRequirement_WithNullEntryInImplements_FailsValidation()
    {
        var requirement = new RuleRequirement { Implements = new TargetSelector { Interfaces = ["IFoo", null!] } };

        new RuleRequirementValidator(new TargetSelectorValidator()).Validate(requirement).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RuleDefinition_Require_DoesNotNeedATo()
    {
        var rule = new RuleDefinition
        {
            Id = "r1",
            Because = "why",
            Kind = RuleKind.Require,
            From = new TargetSelector { Interfaces = ["IHandler"] },
            Require = new RuleRequirement { Layer = "Application" },
        };

        rule.To.Should().NotBeNull();
        RuleValidator().Validate(rule).IsValid.Should().BeTrue();
    }

    [Fact]
    public void RuleRequirement_DefaultsToNoRequirements()
    {
        var requirement = new RuleRequirement();

        requirement.Layer.Should().BeNull();
        requirement.Kind.Should().BeNull();
        requirement.Accessibility.Should().BeNull();
        requirement.Implements.Should().BeNull();
    }

    [Fact]
    public void Members_NullEntry_FailsValidation()
    {
        var selector = new TargetSelector { Members = ["Foo", null!] };

        new TargetSelectorValidator().Validate(selector).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Packages_NullEntry_FailsValidation()
    {
        var rule = new RuleDefinition
        {
            Id = "R1",
            Because = "no banned packages",
            Kind = RuleKind.BannedPackages,
            From = new TargetSelector(),
            Packages = ["Foo.*", null!],
        };

        RuleValidator().Validate(rule).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RuleKind_NewValues_AreFourAndFive()
    {
        ((int)RuleKind.BannedPackages).Should().Be(4);
        ((int)RuleKind.UniqueNames).Should().Be(5);
    }

    [Fact]
    public void Against_Default_IsEmptySelector()
    {
        var rule = new RuleDefinition { Id = "R1", Because = "b", Kind = RuleKind.UniqueNames, From = new TargetSelector() };

        rule.Against.Layers.Should().BeEmpty();
        rule.Against.Members.Should().BeEmpty();
        rule.Packages.Should().BeEmpty();
    }

    private static RuleDefinitionValidator RuleValidator() =>
        new(new TargetSelectorValidator(), new TargetSelectorValidator(), new RuleRequirementValidator(new TargetSelectorValidator()), new TargetSelectorValidator(), new RuleExceptionValidator());

    private static RulePackValidator PackValidator() => new(
        new LayerDefinitionValidator(),
        new ComponentRoleDefinitionValidator(),
        new RuleDefinitionValidator(new TargetSelectorValidator(), new TargetSelectorValidator(), new RuleRequirementValidator(new TargetSelectorValidator()), new TargetSelectorValidator(), new RuleExceptionValidator()),
        new LayerRetargetValidator(),
        new RuleAmendmentValidator(new RuleExceptionValidator()),
        new NamingConventionsValidator());
}
