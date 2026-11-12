using Ariadne.NET.Abstractions;
using FluentAssertions;

namespace Ariadne.NET.Tests.Abstractions;

public sealed class TargetSelectorTraitTests
{
    [Fact]
    public void Defaults_constrain_nothing()
    {
        var selector = new TargetSelector();

        selector.Names.Should().BeEmpty();
        selector.Kinds.Should().BeEmpty();
        selector.Accessibility.Should().BeEmpty();
        selector.AssignableTo.Should().BeEmpty();
        selector.Sealed.Should().BeNull();
        selector.Abstract.Should().BeNull();
        selector.Static.Should().BeNull();
        selector.Nested.Should().BeNull();

        var requirement = new RuleRequirement();
        requirement.Is.Should().BeNull();
        requirement.IsNot.Should().BeNull();
    }

    [Fact]
    public void Validator_rejects_null_entries_in_new_lists()
    {
        var validator = new TargetSelectorValidator();

        var result = validator.Validate(new TargetSelector
        {
            Names = [null!],
            Accessibility = [null!],
            AssignableTo = [null!],
        });

        result.IsValid.Should().BeFalse();
        var names = PropertyNames(result);
        names.Should().Contain(["Names", "Accessibility", "AssignableTo"]);
    }

    [Fact]
    public void Kind_values_are_appended()
    {
        ((int)CSharpTypeKind.Enum).Should().Be(5);
        ((int)CSharpTypeKind.Delegate).Should().Be(6);
    }

    [Fact]
    public void Exception_traits_default_to_nothing()
    {
        var exception = new RuleException { Because = "x" };

        exception.Names.Should().BeEmpty();
        exception.Kinds.Should().BeEmpty();
        exception.Accessibility.Should().BeEmpty();
        exception.AssignableTo.Should().BeEmpty();
        exception.Sealed.Should().BeNull();
        exception.Abstract.Should().BeNull();
        exception.Static.Should().BeNull();
        exception.Nested.Should().BeNull();

        var result = new RuleExceptionValidator().Validate(
            new RuleException { Because = "x", Names = [null!], Accessibility = [null!], AssignableTo = [null!] });
        result.IsValid.Should().BeFalse();
        PropertyNames(result).Should().Contain(["Names", "Accessibility", "AssignableTo"]);
    }

    [Fact]
    public void Code_built_role_and_require_reject_enum_and_delegate()
    {
        var roleValidator = new ComponentRoleDefinitionValidator();
        var requirementValidator = new RuleRequirementValidator(new TargetSelectorValidator(), new TargetSelectorValidator(), new TargetSelectorValidator());

        foreach (var kind in new[] { CSharpTypeKind.Enum, CSharpTypeKind.Delegate })
        {
            PropertyNames(roleValidator.Validate(new ComponentRoleDefinition { Name = "R", Layer = "L", TypeKind = kind }))
                .Should().Contain("TypeKind");
            PropertyNames(requirementValidator.Validate(new RuleRequirement { Kind = kind }))
                .Should().Contain("Kind");
        }

        foreach (var kind in new[] { CSharpTypeKind.Class, CSharpTypeKind.Interface, CSharpTypeKind.Record, CSharpTypeKind.Struct, CSharpTypeKind.RecordStruct })
        {
            roleValidator.Validate(new ComponentRoleDefinition { Name = "R", Layer = "L", TypeKind = kind }).IsValid.Should().BeTrue();
            requirementValidator.Validate(new RuleRequirement { Kind = kind }).IsValid.Should().BeTrue();
        }
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
}
