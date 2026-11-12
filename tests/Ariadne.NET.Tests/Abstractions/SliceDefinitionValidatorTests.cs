using Ariadne.NET.Abstractions;
using FluentAssertions;

namespace Ariadne.NET.Tests.Abstractions;

public sealed class SliceDefinitionValidatorTests
{
    private const string ConditionMessage = "Exactly one of freeOfCycles and independent must be true.";

    [Fact]
    public void Valid_definition_passes()
    {
        var result = new SliceDefinitionValidator(new TargetSelectorValidator())
            .Validate(new SliceDefinition { Matching = ["Shop.(*)"], Independent = true });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_matching_fails()
    {
        var result = new SliceDefinitionValidator(new TargetSelectorValidator())
            .Validate(new SliceDefinition { Matching = [], Independent = true });

        result.IsValid.Should().BeFalse();
        PropertyNames(result).Should().Contain("Matching");
    }

    [Fact]
    public void Null_entry_fails()
    {
        var result = new SliceDefinitionValidator(new TargetSelectorValidator())
            .Validate(new SliceDefinition { Matching = ["Shop.(*)", null!], Independent = true });

        result.IsValid.Should().BeFalse();
        PropertyNames(result).Should().Contain("Matching");
    }

    [Fact]
    public void Both_conditions_fail()
    {
        var result = new SliceDefinitionValidator(new TargetSelectorValidator())
            .Validate(new SliceDefinition { Matching = ["Shop.(*)"], FreeOfCycles = true, Independent = true });

        result.IsValid.Should().BeFalse();
        Messages(result).Should().Contain(ConditionMessage);
    }

    [Fact]
    public void Neither_condition_fails()
    {
        var result = new SliceDefinitionValidator(new TargetSelectorValidator())
            .Validate(new SliceDefinition { Matching = ["Shop.(*)"] });

        result.IsValid.Should().BeFalse();
        Messages(result).Should().Contain(ConditionMessage);
    }

    [Fact]
    public void Same_rule_with_independent_set_is_valid()
    {
        var result = new SliceDefinitionValidator(new TargetSelectorValidator())
            .Validate(new SliceDefinition { Matching = ["Shop.(*)"], Independent = true });

        result.IsValid.Should().BeTrue();
        Messages(result).Should().NotContain(ConditionMessage);
    }

    [Fact]
    public void Slices_kind_is_six_and_rule_defaults_to_no_slices()
    {
        ((int)RuleKind.Slices).Should().Be(6);
        new RuleDefinition { Id = "R", Because = "b", Kind = RuleKind.Forbid, From = new TargetSelector() }.Slices.Should().BeNull();
    }

    [Fact]
    public void Rule_validator_checks_the_nested_slices()
    {
        var validator = new RuleDefinitionValidator(
            new TargetSelectorValidator(),
            new TargetSelectorValidator(),
            new RuleRequirementValidator(new TargetSelectorValidator(), new TargetSelectorValidator(), new TargetSelectorValidator()),
            new TargetSelectorValidator(),
            new RuleExceptionValidator(),
            new SliceDefinitionValidator(new TargetSelectorValidator()));
        var rule = new RuleDefinition
        {
            Id = "R",
            Because = "b",
            Kind = RuleKind.Slices,
            From = new TargetSelector(),
            Slices = new SliceDefinition { Matching = ["Shop.(*)"] },
        };

        validator.Validate(rule).IsValid.Should().BeFalse();
    }

    private static List<string> PropertyNames(ZeroAlloc.Validation.ValidationResult result)
    {
        var names = new List<string>();
        foreach (var failure in result.Failures.ToArray())
        {
            names.Add(failure.PropertyName);
        }

        return names;
    }

    private static List<string> Messages(ZeroAlloc.Validation.ValidationResult result)
    {
        var messages = new List<string>();
        foreach (var failure in result.Failures.ToArray())
        {
            messages.Add(failure.ErrorMessage);
        }

        return messages;
    }
}
