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

    /// <summary>The rule line, the exception count when there are exceptions, and the because line last.</summary>
    public static IReadOnlyList<string> DescribeRule(RuleDefinition rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var kind = rule.Kind switch
        {
            RuleKind.Forbid => "forbid",
            RuleKind.AllowOnly => "allowOnly",
            RuleKind.Require => "require",
            _ => rule.Kind.ToString(),
        };
        var target = rule.Kind == RuleKind.Require ? Describe(rule.Require) : Describe(rule.To);
        var lines = new List<string> { $"{rule.Id} {kind} {Describe(rule.From)} -> {target}" };
        if (rule.Exceptions.Count > 0)
        {
            lines.Add($"{rule.Exceptions.Count} exception(s)");
        }

        lines.Add(rule.Because);
        return lines;
    }

    public static string Describe(TargetSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return Describe(
            selector.Namespaces,
            selector.Assemblies,
            selector.Layers,
            selector.Types,
            selector.BaseTypes,
            selector.Interfaces,
            selector.Attributes);
    }

    // Members in the order layer, kind, accessibility, implements, joined by ", ".
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

        return parts.Count > 0 ? string.Join(", ", parts) : "no requirement";
    }

    // The enum name with a lower-case first letter is the spelling a pack uses: class, recordStruct.
    private static string YamlSpelling(CSharpTypeKind kind)
    {
        var name = kind.ToString();
        return string.Concat(char.ToLowerInvariant(name[0]).ToString(), name.AsSpan(1));
    }

    // Kinds in the order layers, namespaces, assemblies, types, baseTypes, interfaces, attributes; kinds joined by "; ", values by ", ".
    private static string Describe(
        IList<string> namespaces,
        IList<string> assemblies,
        IList<string>? layers = null,
        IList<string>? types = null,
        IList<string>? baseTypes = null,
        IList<string>? interfaces = null,
        IList<string>? attributes = null)
    {
        var parts = new List<string>();
        AddKind(parts, "layers", layers);
        AddKind(parts, "namespaces", namespaces);
        AddKind(parts, "assemblies", assemblies);
        AddKind(parts, "types", types);
        AddKind(parts, "baseTypes", baseTypes);
        AddKind(parts, "interfaces", interfaces);
        AddKind(parts, "attributes", attributes);
        return parts.Count > 0 ? string.Join("; ", parts) : "no selectors";
    }

    private static void AddKind(List<string> parts, string label, IList<string>? values)
    {
        if (values is { Count: > 0 })
        {
            parts.Add($"{label} {string.Join(", ", values)}");
        }
    }
}
