using Ariadne.NET.Abstractions;
using FluentAssertions;

namespace Ariadne.NET.Tests.Abstractions;

public sealed class AttributeArgumentMatchTests
{
    [Fact]
    public void Entry_without_arguments_or_named_is_invalid()
    {
        var result = new AttributeArgumentMatchValidator().Validate(new AttributeArgumentMatch { Attribute = "A.*" });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Entry_with_null_argument_is_invalid()
    {
        var result = new AttributeArgumentMatchValidator().Validate(new AttributeArgumentMatch { Arguments = ["a", null!] });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Entry_with_null_named_value_is_invalid()
    {
        var named = new Dictionary<string, string>(StringComparer.Ordinal) { ["Key"] = null! };

        var result = new AttributeArgumentMatchValidator().Validate(new AttributeArgumentMatch { Named = named });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Entry_with_null_arguments_is_invalid_without_throwing()
    {
        var withoutNamed = () => new AttributeArgumentMatchValidator().Validate(new AttributeArgumentMatch { Arguments = null! });
        var withNamed = () => new AttributeArgumentMatchValidator().Validate(
            new AttributeArgumentMatch { Arguments = null!, Named = new Dictionary<string, string>(StringComparer.Ordinal) { ["Key"] = "v" } });

        withoutNamed.Should().NotThrow().Which.IsValid.Should().BeFalse();
        withNamed.Should().NotThrow().Which.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Entry_with_null_named_is_invalid()
    {
        var result = new AttributeArgumentMatchValidator().Validate(new AttributeArgumentMatch { Arguments = ["a"], Named = null! });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Entry_with_named_only_is_valid()
    {
        var named = new Dictionary<string, string>(StringComparer.Ordinal) { ["Key"] = "v" };

        var result = new AttributeArgumentMatchValidator().Validate(new AttributeArgumentMatch { Named = named });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Entry_with_arguments_only_is_valid()
    {
        new AttributeArgumentMatchValidator().Validate(new AttributeArgumentMatch { Arguments = ["x"] }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Selector_with_null_attribute_argument_entry_is_invalid()
    {
        var result = new TargetSelectorValidator(new AttributeArgumentMatchValidator()).Validate(new TargetSelector { AttributeArguments = [null!] });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Selector_with_invalid_attribute_argument_entry_is_invalid()
    {
        var result = new TargetSelectorValidator(new AttributeArgumentMatchValidator()).Validate(new TargetSelector { AttributeArguments = [new AttributeArgumentMatch()] });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Selector_with_null_only_attributes_entry_is_invalid()
    {
        new TargetSelectorValidator(new AttributeArgumentMatchValidator()).Validate(new TargetSelector { OnlyAttributes = [null!] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Exception_with_null_attribute_argument_entry_is_invalid()
    {
        var result = new RuleExceptionValidator(new AttributeArgumentMatchValidator()).Validate(new RuleException { Because = "b", AttributeArguments = [null!] });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Exception_with_invalid_attribute_argument_entry_is_invalid()
    {
        var result = new RuleExceptionValidator(new AttributeArgumentMatchValidator()).Validate(new RuleException { Because = "b", AttributeArguments = [new AttributeArgumentMatch()] });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Exception_with_null_only_attributes_entry_is_invalid()
    {
        new RuleExceptionValidator(new AttributeArgumentMatchValidator()).Validate(new RuleException { Because = "b", OnlyAttributes = [null!] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Defaults_are_empty()
    {
        var selector = new TargetSelector();
        selector.AttributeArguments.Should().BeEmpty();
        selector.OnlyAttributes.Should().BeEmpty();
        var match = new AttributeArgumentMatch();
        match.Attribute.Should().BeNull();
        match.Arguments.Should().BeEmpty();
        match.Named.Should().BeEmpty();
    }
}
