using System.Globalization;
using System.Text;
using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>
/// One key of a require rule's <c>is</c> or <c>isNot</c> condition, compiled into a selector that holds that key alone. An
/// <c>is</c> key fails a type it does not match; an <c>isNot</c> key fails a type it matches. The key is named as in YAML.
/// </summary>
internal sealed class RequireCondition
{
    private readonly bool _negated;
    private readonly bool _phraseNegated;
    private readonly string _want;

    private RequireCondition(string key, TypeSelector selector, bool negated, bool phraseNegated, string want)
    {
        Key = key;
        Selector = selector;
        _negated = negated;
        _phraseNegated = phraseNegated;
        _want = want;
    }

    /// <summary>The YAML key, such as <c>names</c> or <c>sealed</c>.</summary>
    public string Key { get; }

    /// <summary>The selector that holds this key alone.</summary>
    public TypeSelector Selector { get; }

    /// <summary>
    /// Compiles every set key of <paramref name="isCondition"/>, then of <paramref name="isNotCondition"/>, in a fixed key order.
    /// <c>layers</c> and <c>members</c> are not conditions and are left out; the loader rejects them.
    /// </summary>
    /// <param name="isCondition">The <c>is</c> condition, or null.</param>
    /// <param name="isNotCondition">The <c>isNot</c> condition, or null.</param>
    /// <returns>The conditions, empty when neither is set.</returns>
    public static RequireCondition[] Compile(TargetSelector? isCondition, TargetSelector? isNotCondition)
    {
        var conditions = new List<RequireCondition>();
        if (isCondition is not null)
        {
            Add(isCondition, negated: false, conditions);
        }

        if (isNotCondition is not null)
        {
            Add(isNotCondition, negated: true, conditions);
        }

        return [.. conditions];
    }

    /// <summary>Whether the type breaks this key.</summary>
    /// <param name="type">The selected type.</param>
    /// <param name="names">The names of the check.</param>
    /// <returns>True when an <c>is</c> key does not match, or an <c>isNot</c> key matches.</returns>
    public bool Fails(INamedTypeSymbol type, TypeNames names) => Selector.Matches(type, names) == _negated;

    /// <summary>The violation message for a type that breaks this key.</summary>
    /// <param name="type">The type.</param>
    /// <param name="ruleId">The rule id.</param>
    /// <param name="names">The names of the check.</param>
    /// <returns>The message.</returns>
    public string Message(INamedTypeSymbol type, string ruleId, TypeNames names)
    {
        var not = _phraseNegated ? "not " : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Type '{type.Name}' is selected by rule '{ruleId}' and should {not}{_want} but {Found(type, names)}");
    }

    private string Found(INamedTypeSymbol type, TypeNames names) => Key switch
    {
        "namespaces" => names.NamespaceOf(type) is { Length: > 0 } ns
            ? string.Create(CultureInfo.InvariantCulture, $"is in namespace '{ns}'")
            : "is in the global namespace",
        "assemblies" => string.Create(CultureInfo.InvariantCulture, $"is in assembly '{type.ContainingAssembly?.Name}'"),
        "types" => string.Create(CultureInfo.InvariantCulture, $"is named '{names.FullNameOf(type)}'"),
        "names" => string.Create(CultureInfo.InvariantCulture, $"is named '{type.Name}'"),
        "kinds" => string.Create(CultureInfo.InvariantCulture, $"is a '{TypeTraits.KindOf(type)}'"),
        "accessibility" => string.Create(CultureInfo.InvariantCulture, $"is '{RoleRules.Keyword(type.DeclaredAccessibility)}'"),
        "baseTypes" or "interfaces" or "attributes" => _phraseNegated ? "does" : "does not",
        _ => _phraseNegated ? "is" : "is not",
    };

    private static void Add(TargetSelector selector, bool negated, List<RequireCondition> conditions)
    {
        AddList(conditions, "namespaces", selector.Namespaces, negated, "be in a namespace matching ", static s => new() { Namespaces = s });
        AddList(conditions, "assemblies", selector.Assemblies, negated, "be in an assembly matching ", static s => new() { Assemblies = s });
        AddList(conditions, "types", selector.Types, negated, "have a full name matching ", static s => new() { Types = s });
        AddList(conditions, "baseTypes", selector.BaseTypes, negated, "derive from a class matching ", static s => new() { BaseTypes = s });
        AddList(conditions, "interfaces", selector.Interfaces, negated, "implement an interface matching ", static s => new() { Interfaces = s });
        AddList(conditions, "attributes", selector.Attributes, negated, "have an attribute matching ", static s => new() { Attributes = s });
        AddList(conditions, "names", selector.Names, negated, "have a name matching ", static s => new() { Names = s });

        if (selector.Kinds.Count > 0)
        {
            var kinds = new List<string>(selector.Kinds.Count);
            foreach (var kind in selector.Kinds)
            {
                kinds.Add(kind.ToString());
            }

            conditions.Add(new RequireCondition(
                "kinds", TypeSelector.From(new TargetSelector { Kinds = selector.Kinds }), negated, negated, "be a " + Quoted(kinds)));
        }

        AddList(conditions, "accessibility", selector.Accessibility, negated, "be ", static s => new() { Accessibility = s });
        AddList(conditions, "assignableTo", selector.AssignableTo, negated, "be assignable to a type matching ", static s => new() { AssignableTo = s });

        AddFlag(conditions, "sealed", selector.Sealed, negated, static v => new() { Sealed = v });
        AddFlag(conditions, "abstract", selector.Abstract, negated, static v => new() { Abstract = v });
        AddFlag(conditions, "static", selector.Static, negated, static v => new() { Static = v });
        AddFlag(conditions, "nested", selector.Nested, negated, static v => new() { Nested = v });
    }

    private static void AddList(
        List<RequireCondition> conditions, string key, IList<string> values, bool negated, string wantPrefix, Func<IList<string>, TargetSelector> only)
    {
        if (values.Count > 0)
        {
            conditions.Add(new RequireCondition(key, TypeSelector.From(only(values)), negated, negated, wantPrefix + Quoted(values)));
        }
    }

    // A flag reads as its positive form: sealed: false under is says "should not be sealed", the same as sealed: true under isNot.
    private static void AddFlag(List<RequireCondition> conditions, string key, bool? value, bool negated, Func<bool, TargetSelector> only)
    {
        if (value is { } wanted)
        {
            conditions.Add(new RequireCondition(key, TypeSelector.From(only(wanted)), negated, phraseNegated: wanted == negated, "be " + key));
        }
    }

    private static string Quoted(IList<string> values)
    {
        var text = new StringBuilder();
        for (var i = 0; i < values.Count; i++)
        {
            text.Append(i == 0 ? "'" : " or '").Append(values[i]).Append('\'');
        }

        return text.ToString();
    }
}
