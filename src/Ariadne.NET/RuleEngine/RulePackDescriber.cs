using System.Text;
using Ariadne.NET.Abstractions;

namespace Ariadne.NET.RuleEngine;

/// <summary>
/// Renders layers, rules, selectors and requirements as the one-line text <c>ariadne rules</c> prints.
/// Returned strings carry no indent; callers add their own.
/// </summary>
public static class RulePackDescriber
{
    /// <summary>The <c>Name [Id]: selectors; depends on …</c> line for a layer.</summary>
    public static string DescribeLayer(LayerDefinition layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        var deps = layer.AllowedDependencies.Count > 0
            ? string.Join(", ", layer.AllowedDependencies)
            : "nothing";
        var selectors = Describe(layer.Namespaces, layer.Assemblies);
        return $"{layer.Name} [{layer.Id}]: {selectors}; depends on {deps}";
    }

    /// <summary>The rule line, the exception count when there are exceptions, <c>allowEmpty</c> when the rule may match nothing, <c>requireTarget</c> when its target must match something, and the because line last.</summary>
    public static IReadOnlyList<string> DescribeRule(RuleDefinition rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var kind = rule.Kind switch
        {
            RuleKind.Forbid => "forbid",
            RuleKind.AllowOnly => "allowOnly",
            RuleKind.Require => "require",
            RuleKind.BannedPackages => "bannedPackages",
            RuleKind.UniqueNames => "uniqueNames",
            RuleKind.Slices => "slices",
            _ => rule.Kind.ToString(),
        };
        var target = rule.Kind switch
        {
            RuleKind.Require => Describe(rule.Require),
            RuleKind.BannedPackages => $"packages {string.Join(", ", rule.Packages)}",
            RuleKind.UniqueNames => $"against {Describe(rule.Against)}",
            _ => Describe(rule.To),
        };
        var head = rule.Kind == RuleKind.Slices
            ? $"{rule.Id} {kind} {Describe(rule.Slices)}"
            : $"{rule.Id} {kind} {Describe(rule.From)} -> {target}";
        var lines = new List<string> { head };
        if (rule.Exceptions.Count > 0)
        {
            lines.Add($"{rule.Exceptions.Count} exception(s)");
        }

        if (rule.AllowEmpty)
        {
            lines.Add("allowEmpty");
        }

        if (rule.RequireTarget)
        {
            lines.Add("requireTarget");
        }

        lines.Add(rule.Because);
        return lines;
    }

    /// <summary>The <c>matching</c> patterns, the condition and the <c>allow</c> selector of a slices rule, as <c>matching A, B -> independent; allow …</c>.</summary>
    public static string Describe(SliceDefinition? slices)
    {
        if (slices is null)
        {
            return "no slices";
        }

        var condition = slices.Independent ? "independent" : slices.FreeOfCycles ? "freeOfCycles" : "no condition";
        var text = $"matching {string.Join(", ", slices.Matching)} -> {condition}";
        return slices.Allow is null ? text : $"{text}; allow {Describe(slices.Allow)}";
    }

    /// <summary>
    /// The selector kinds that are set, joined by "; ", or <c>no selectors</c>. <c>dependsOn</c> and <c>onlyDependsOn</c> come
    /// last, each with its nested selector in parentheses.
    /// </summary>
    public static string Describe(TargetSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Describe(selector, quoted: false);
    }

    /// <summary>
    /// The selector as <see cref="Describe(TargetSelector)"/> gives it, with each listed value in single quotes, as the
    /// violation messages quote what a rule names: <c>layers 'Domain'</c>.
    /// </summary>
    internal static string DescribeQuoted(TargetSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Describe(selector, quoted: true);
    }

    private static string Describe(TargetSelector selector, bool quoted) => Describe(
        selector.Namespaces,
        selector.Assemblies,
        selector.Layers,
        selector.Types,
        selector.BaseTypes,
        selector.Interfaces,
        selector.Attributes,
        selector.Members,
        selector,
        quoted);

    /// <summary>The requirement members in the order layer, kind, accessibility, implements, is, isNot, joined by ", ", or <c>no requirement</c>.</summary>
    public static string Describe(RuleRequirement? requirement)
    {
        if (requirement is null)
        {
            return "no requirement";
        }

        var parts = new List<string>();
        if (requirement.Layer is not null)
        {
            parts.Add($"layer {requirement.Layer}");
        }

        if (requirement.Kind is { } kind)
        {
            parts.Add($"kind {YamlSpelling(kind)}");
        }

        if (requirement.Accessibility is not null)
        {
            parts.Add(requirement.Accessibility);
        }

        if (requirement.Implements is not null)
        {
            parts.Add($"implements {Describe(requirement.Implements)}");
        }

        if (requirement.Is is not null)
        {
            parts.Add($"is {Describe(requirement.Is)}");
        }

        if (requirement.IsNot is not null)
        {
            parts.Add($"isNot {Describe(requirement.IsNot)}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "no requirement";
    }

    // The enum name with a lower-case first letter is the spelling a pack uses: class, recordStruct.
    private static string YamlSpelling(CSharpTypeKind kind)
    {
        var name = kind.ToString();
        return string.Concat(char.ToLowerInvariant(name[0]).ToString(), name.AsSpan(1));
    }

    // Kinds in the order layers, namespaces, assemblies, types, baseTypes, interfaces, attributes, attributeArguments, onlyAttributes, members, then the traits names, kinds, accessibility, assignableTo, sealed, abstract, static, nested, then dependsOn and onlyDependsOn; kinds joined by "; ", values by ", ".
    private static string Describe(
        IList<string> namespaces,
        IList<string> assemblies,
        IList<string>? layers = null,
        IList<string>? types = null,
        IList<string>? baseTypes = null,
        IList<string>? interfaces = null,
        IList<string>? attributes = null,
        IList<string>? members = null,
        TargetSelector? traits = null,
        bool quoted = false)
    {
        var parts = new List<string>();
        AddKind(parts, "layers", layers, quoted);
        AddKind(parts, "namespaces", namespaces, quoted);
        AddKind(parts, "assemblies", assemblies, quoted);
        AddKind(parts, "types", types, quoted);
        AddKind(parts, "baseTypes", baseTypes, quoted);
        AddKind(parts, "interfaces", interfaces, quoted);
        AddKind(parts, "attributes", attributes, quoted);
        if (traits is not null)
        {
            AddAttributeArguments(parts, traits.AttributeArguments);
            AddKind(parts, "onlyAttributes", traits.OnlyAttributes, quoted);
        }

        AddKind(parts, "members", members, quoted);
        if (traits is not null)
        {
            AddKind(parts, "names", traits.Names, quoted);
            AddKind(parts, "kinds", traits.Kinds.Select(YamlSpelling).ToList(), quoted);
            AddKind(parts, "accessibility", traits.Accessibility, quoted);
            AddKind(parts, "assignableTo", traits.AssignableTo, quoted);
            AddFlag(parts, "sealed", traits.Sealed);
            AddFlag(parts, "abstract", traits.Abstract);
            AddFlag(parts, "static", traits.Static);
            AddFlag(parts, "nested", traits.Nested);
            AddNested(parts, "dependsOn", traits.DependsOn, quoted);
            AddNested(parts, "onlyDependsOn", traits.OnlyDependsOn, quoted);
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "no selectors";
    }

    private static void AddAttributeArguments(List<string> parts, IList<AttributeArgumentMatch> matches)
    {
        if (matches.Count > 0)
        {
            parts.Add($"attributeArguments {string.Join(" or ", matches.Select(DescribeMatch))}");
        }
    }

    private static string DescribeMatch(AttributeArgumentMatch match)
    {
        var text = new StringBuilder(match.Attribute ?? "any attribute");
        if (match.Arguments.Count > 0)
        {
            text.Append(" with arguments ").Append(string.Join(", ", match.Arguments));
        }

        if (match.Named.Count > 0)
        {
            text.Append(match.Arguments.Count > 0 ? " and named arguments " : " with named arguments ")
                .Append(string.Join(", ", match.Named.Select(static pair => $"{pair.Key}={pair.Value}")));
        }

        return text.ToString();
    }

    private static void AddKind(List<string> parts, string label, IList<string>? values, bool quoted)
    {
        if (values is { Count: > 0 })
        {
            parts.Add(quoted
                ? $"{label} {string.Join(", ", values.Select(static v => $"'{v}'"))}"
                : $"{label} {string.Join(", ", values)}");
        }
    }

    private static void AddNested(List<string> parts, string label, TargetSelector? nested, bool quoted)
    {
        if (nested is not null)
        {
            parts.Add($"{label} ({Describe(nested, quoted)})");
        }
    }

    private static void AddFlag(List<string> parts, string label, bool? value)
    {
        if (value is { } flag)
        {
            parts.Add(flag ? $"{label} true" : $"{label} false");
        }
    }
}
