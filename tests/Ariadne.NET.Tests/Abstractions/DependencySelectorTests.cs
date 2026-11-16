using Ariadne.NET.Abstractions;
using FluentAssertions;

namespace Ariadne.NET.Tests.Abstractions;

public sealed class DependencySelectorTests
{
    private static ZeroAlloc.Validation.ValidationResult Selector(TargetSelector s) =>
        new TargetSelectorValidator(new AttributeArgumentMatchValidator()).Validate(s);

    private static ZeroAlloc.Validation.ValidationResult Exception(RuleException e) =>
        new RuleExceptionValidator(new AttributeArgumentMatchValidator()).Validate(e);

    private static RuleException Ex(TargetSelector? dependsOn = null, TargetSelector? only = null) =>
        new() { Because = "b", DependsOn = dependsOn, OnlyDependsOn = only };

    private static TargetSelector Ns(params string[] ns) => new() { Namespaces = ns };

    [Fact]
    public void Selector_with_nested_dependsOn_inside_dependsOn_is_invalid()
    {
        Selector(new TargetSelector { DependsOn = new TargetSelector { Namespaces = ["A"], DependsOn = Ns("B") } }).IsValid.Should().BeFalse();
        Selector(new TargetSelector { DependsOn = new TargetSelector { Namespaces = ["A"], OnlyDependsOn = Ns("B") } }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Selector_with_members_in_dependsOn_is_invalid() =>
        Selector(new TargetSelector { DependsOn = new TargetSelector { Members = ["M"] } }).IsValid.Should().BeFalse();

    [Fact]
    public void Selector_with_empty_dependsOn_is_invalid()
    {
        Selector(new TargetSelector { DependsOn = new TargetSelector() }).IsValid.Should().BeFalse();
        Selector(new TargetSelector { OnlyDependsOn = new TargetSelector() }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Selector_with_layers_and_namespaces_in_dependsOn_is_invalid() =>
        Selector(new TargetSelector { DependsOn = new TargetSelector { Layers = ["L"], Namespaces = ["A"] } }).IsValid.Should().BeFalse();

    [Fact]
    public void Selector_with_valid_onlyDependsOn_is_valid()
    {
        Selector(new TargetSelector { OnlyDependsOn = Ns("A.**") }).IsValid.Should().BeTrue();
        Selector(new TargetSelector { Layers = ["X"], DependsOn = new TargetSelector { Layers = ["L"], Sealed = true } }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Selector_with_null_entry_in_dependsOn_is_invalid() =>
        Selector(new TargetSelector { DependsOn = new TargetSelector { Namespaces = [null!] } }).IsValid.Should().BeFalse();

    [Fact]
    public void Exception_with_nested_dependsOn_inside_dependsOn_is_invalid() =>
        Exception(Ex(new TargetSelector { Namespaces = ["A"], DependsOn = Ns("B") })).IsValid.Should().BeFalse();

    [Fact]
    public void Exception_with_members_in_dependsOn_is_invalid() =>
        Exception(Ex(new TargetSelector { Members = ["M"] })).IsValid.Should().BeFalse();

    [Fact]
    public void Exception_with_empty_dependsOn_is_invalid()
    {
        Exception(Ex(new TargetSelector())).IsValid.Should().BeFalse();
        Exception(Ex(only: new TargetSelector())).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Exception_with_layers_and_namespaces_in_dependsOn_is_invalid() =>
        Exception(Ex(new TargetSelector { Layers = ["L"], Namespaces = ["A"] })).IsValid.Should().BeFalse();

    [Fact]
    public void Exception_with_valid_onlyDependsOn_is_valid()
    {
        Exception(Ex(only: Ns("A.**"))).IsValid.Should().BeTrue();
        Exception(Ex(new TargetSelector { Layers = ["L"] })).IsValid.Should().BeTrue();
    }
}
