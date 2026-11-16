using System.Reflection;
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

    [Fact]
    public void Selector_with_an_invalid_attributeArguments_entry_in_dependsOn_is_invalid() =>
        Selector(new TargetSelector { DependsOn = new TargetSelector { AttributeArguments = [new AttributeArgumentMatch()] } }).IsValid.Should().BeFalse();

    [Fact]
    public void Selector_with_a_null_attributeArguments_entry_in_dependsOn_is_invalid() =>
        Selector(new TargetSelector { OnlyDependsOn = new TargetSelector { AttributeArguments = [null!] } }).IsValid.Should().BeFalse();

    [Fact]
    public void Exception_with_an_invalid_attributeArguments_entry_in_dependsOn_is_invalid() =>
        Exception(Ex(new TargetSelector { AttributeArguments = [new AttributeArgumentMatch()] })).IsValid.Should().BeFalse();

    [Fact]
    public void Exception_with_a_null_attributeArguments_entry_in_dependsOn_is_invalid() =>
        Exception(Ex(only: new TargetSelector { AttributeArguments = [null!] })).IsValid.Should().BeFalse();

    private const string BadDependencyMessage =
        " must select something, must not hold members, dependsOn or onlyDependsOn, and must not mix layers with namespace, assembly or type globs.";

    [Fact]
    public void Selector_and_exception_give_the_same_message_for_a_bad_dependency_selector()
    {
        Messages(Selector(new TargetSelector { DependsOn = new TargetSelector() })).Should().Equal("DependsOn" + BadDependencyMessage);
        Messages(Selector(new TargetSelector { OnlyDependsOn = new TargetSelector() })).Should().Equal("OnlyDependsOn" + BadDependencyMessage);
        Messages(Exception(Ex(new TargetSelector()))).Should().Equal("DependsOn" + BadDependencyMessage);
        Messages(Exception(Ex(only: new TargetSelector()))).Should().Equal("OnlyDependsOn" + BadDependencyMessage);
    }

    // The nested selector may not hold these; every other key selects something on its own.
    private static readonly string[] KeysTheNestedSelectorRejects = ["Members", "DependsOn", "OnlyDependsOn"];

    public static TheoryData<string> SelectorProperties()
    {
        var data = new TheoryData<string>();
        foreach (var property in typeof(TargetSelector).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            data.Add(property.Name);
        }

        return data;
    }

    // Each public key of TargetSelector, set alone, is either a key the nested selector rejects or one that makes it
    // select something. A new key that the hand-listed checks forget reads as empty, and fails here.
    [Theory]
    [MemberData(nameof(SelectorProperties))]
    public void Every_selector_key_set_alone_is_seen_by_the_dependency_check(string name)
    {
        var property = typeof(TargetSelector).GetProperty(name)!;
        var nested = new TargetSelector();
        property.SetValue(nested, SampleValue(property.PropertyType, name));

        var expected = Array.IndexOf(KeysTheNestedSelectorRejects, name) < 0;
        Selector(new TargetSelector { DependsOn = nested }).IsValid.Should().Be(expected, because: $"'{name}' alone in dependsOn");
        Exception(Ex(only: nested)).IsValid.Should().Be(expected, because: $"'{name}' alone in onlyDependsOn of an exception");
    }

    private static object SampleValue(Type type, string name) => type switch
    {
        _ when type == typeof(IList<string>) => string.Equals(name, "Accessibility", StringComparison.Ordinal) ? ["public"] : new List<string> { "Shop.**" },
        _ when type == typeof(IList<CSharpTypeKind>) => new List<CSharpTypeKind> { CSharpTypeKind.Class },
        _ when type == typeof(IList<AttributeArgumentMatch>) => new List<AttributeArgumentMatch> { new() { Attribute = "Shop.RouteAttribute", Arguments = ["x"] } },
        _ when type == typeof(bool?) => true,
        _ when type == typeof(TargetSelector) => Ns("Shop.**"),
        _ => throw new InvalidOperationException($"Add a sample value for TargetSelector.{name} of type {type}."),
    };

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
