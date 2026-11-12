using Ariadne.NET.Abstractions;

using ZeroAlloc.Results;

namespace Ariadne.NET.RuleLoader;

/// <summary>
/// Cross-reference rules for a complete, merged rule pack. A lone user pack may rely on <c>extends</c> for the layers
/// its roles name, so these rules cannot run at load time; the resolver runs them on the result of every resolve.
/// Names compare case-insensitively, as <c>LayerInferenceEngine</c> and <c>CodeGeneratorService</c>
/// match them.
/// </summary>
internal static class RulePackReferenceCheck
{
    public static Result<RulePack, AriadneError> Check(RulePack mergedPack)
    {
        ArgumentNullException.ThrowIfNull(mergedPack);

        var failures = new List<string>();

        var layerNames = CollectNames(mergedPack.Layers.Select(static l => l.Name), "Layers", "layer", failures);
        var roleNames = CollectNames(mergedPack.ComponentRoles.Select(static r => r.Name), "ComponentRoles", "role", failures);

        CheckIds(mergedPack, failures);
        CheckRuleLayers(mergedPack, layerNames, failures);

        for (var i = 0; i < mergedPack.Layers.Count; i++)
        {
            var dependencies = mergedPack.Layers[i].AllowedDependencies;
            for (var d = 0; d < dependencies.Count; d++)
            {
                if (!Contains(layerNames, dependencies[d]))
                {
                    failures.Add($"Layers[{i}].AllowedDependencies[{d}]: refers to unknown layer '{PackText.Show(dependencies[d])}'.");
                }
            }
        }

        for (var i = 0; i < mergedPack.ComponentRoles.Count; i++)
        {
            var role = mergedPack.ComponentRoles[i];

            // Role-only built-in packs such as cqrs and ddd-tactical declare no layers and work on their own, because code
            // generation only reads the role's Layer text. With no layer model there is nothing to refer to.
            if (layerNames.Count > 0 && !Contains(layerNames, role.Layer))
            {
                failures.Add($"ComponentRoles[{i}].Layer: refers to unknown layer '{PackText.Show(role.Layer)}'.");
            }

            // The engine reads InterfaceRole only when it is non-empty, so an empty value means "none".
            if (!string.IsNullOrEmpty(role.InterfaceRole) && !Contains(roleNames, role.InterfaceRole))
            {
                failures.Add($"ComponentRoles[{i}].InterfaceRole: refers to unknown role '{PackText.Show(role.InterfaceRole)}'.");
            }
        }

        return failures.Count == 0
            ? Result<RulePack, AriadneError>.Success(mergedPack)
            : Result<RulePack, AriadneError>.Failure(RulePackError.Invalid(mergedPack.Name, failures));
    }

    // Layers and rules share one id space. A loaded pack always has ids, but a pack built in code reaches the resolver
    // without the load-time member rules, so an empty id is reported here, once, rather than as a duplicate of another.
    private static void CheckIds(RulePack pack, List<string> failures)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < pack.Layers.Count; i++)
        {
            CheckId(pack.Layers[i].Id, "Layers", i, seen, failures);
        }

        for (var i = 0; i < pack.Rules.Count; i++)
        {
            CheckId(pack.Rules[i].Id, "Rules", i, seen, failures);
        }
    }

    private static void CheckId(string? id, string path, int index, HashSet<string> seen, List<string> failures)
    {
        if (string.IsNullOrEmpty(id))
        {
            failures.Add($"{path}[{index}].Id: Id must not be empty.");
        }
        else if (IsReserved(id))
        {
            failures.Add($"id '{PackText.Show(id)}' is reserved for generated rules");
        }
        else if (!seen.Add(id))
        {
            failures.Add($"duplicate id '{PackText.Show(id)}'");
        }
    }

    // CODE-001, CODE-002 and ROLE-<Name> are the ids of the rules generated from component roles.
    private static bool IsReserved(string id) =>
        id.Equals("CODE-001", StringComparison.OrdinalIgnoreCase)
        || id.Equals("CODE-002", StringComparison.OrdinalIgnoreCase)
        || id.StartsWith("ROLE-", StringComparison.OrdinalIgnoreCase);

    private static void CheckRuleLayers(RulePack pack, HashSet<string> layerNames, List<string> failures)
    {
        foreach (var rule in pack.Rules)
        {
            CheckTargetLayers(rule.Id, rule.From, layerNames, failures);
            CheckTargetLayers(rule.Id, rule.To, layerNames, failures);
            CheckTargetLayers(rule.Id, rule.Against, layerNames, failures);
            CheckTraitsOf(rule, failures);

            if (rule.Require?.Layer is { } required && !Contains(layerNames, required))
            {
                failures.Add($"rule '{PackText.Show(rule.Id)}' requires unknown layer '{PackText.Show(required)}'");
            }

            if (rule.Kind == RuleKind.Require)
            {
                CheckRequirement(rule, failures);
            }
        }
    }

    // The loader enforces these for a YAML pack. A pack built in code skips the loader, and the validator would throw on
    // an unknown accessibility or a target mixing layers with code patterns, and an empty exception would exempt every
    // type, so they are reported here. Amendment exceptions are merged in by now, so they are checked too.
    private static void CheckTraitsOf(RuleDefinition rule, List<string> failures)
    {
        var id = PackText.Show(rule.Id);
        CheckTarget($"the {(rule.Kind == RuleKind.Require ? "for" : "from")} of rule '{id}'", rule.From, failures);
        CheckTarget($"the to of rule '{id}'", rule.To, failures);
        CheckTarget($"the against of rule '{id}'", rule.Against, failures);
        if (rule.Require is { } requirement)
        {
            if (requirement.Is is { } isCondition)
            {
                CheckTraitValues($"the is of rule '{id}'", isCondition.Kinds, isCondition.Accessibility, failures);
            }

            if (requirement.IsNot is { } isNotCondition)
            {
                CheckTraitValues($"the isNot of rule '{id}'", isNotCondition.Kinds, isNotCondition.Accessibility, failures);
            }
        }

        for (var i = 0; i < rule.Exceptions.Count; i++)
        {
            var exception = rule.Exceptions[i];
            var where = $"exceptions[{i}] of rule '{id}'";
            if (exception is
                {
                    Namespaces.Count: 0, Assemblies.Count: 0, Types.Count: 0, Names.Count: 0, Kinds.Count: 0, Accessibility.Count: 0,
                    AssignableTo.Count: 0, Sealed: null, Abstract: null, Static: null, Nested: null,
                })
            {
                failures.Add($"{where} selects nothing; it needs at least one of namespaces, assemblies, types or a trait");
            }

            CheckTraitValues(where, exception.Kinds, exception.Accessibility, failures);
        }
    }

    // TargetMatcher compiles a target only when it selects either layers or code patterns; traits may join both.
    private static void CheckTarget(string where, TargetSelector target, List<string> failures)
    {
        if (target.Layers.Count > 0 && (target.Namespaces.Count > 0 || target.Assemblies.Count > 0 || target.Types.Count > 0
                || target.BaseTypes.Count > 0 || target.Interfaces.Count > 0 || target.Attributes.Count > 0 || target.Members.Count > 0))
        {
            failures.Add($"{where} mixes layers with namespaces, assemblies, types, base types, interfaces, attributes or members");
        }

        CheckTraitValues(where, target.Kinds, target.Accessibility, failures);
    }

    private static void CheckTraitValues(string where, IList<CSharpTypeKind> kinds, IList<string> accessibility, List<string> failures)
    {
        foreach (var kind in kinds)
        {
            if (!Enum.IsDefined(kind))
            {
                failures.Add($"{where} has kind '{kind}', which is not a type kind");
            }
        }

        foreach (var keyword in accessibility)
        {
            if (Array.IndexOf(PackYamlReader.AccessibilityKeywords, keyword) < 0)
            {
                failures.Add($"{where} has accessibility '{PackText.Show(keyword)}', which must be one of {string.Join(", ", PackYamlReader.AccessibilityKeywords)}");
            }
        }
    }

    // The loader enforces these for a YAML pack. A pack built in code skips the loader, and RequireRules would silently
    // check nothing for them, so they are reported here.
    private static void CheckRequirement(RuleDefinition rule, List<string> failures)
    {
        var id = PackText.Show(rule.Id);
        if (rule.Require is not { } requirement)
        {
            failures.Add($"rule '{id}' is a require rule and has no requirement");
            return;
        }

        if (requirement.Layer is null && requirement.Kind is null && requirement.Accessibility is null && requirement.Implements is null
            && requirement.Is is null && requirement.IsNot is null)
        {
            failures.Add($"the requirement of rule '{id}' needs at least one of layer, kind, accessibility, implements, is, isNot");
        }

        if (requirement.Kind is { } kind && kind is not (CSharpTypeKind.Class or CSharpTypeKind.Interface or CSharpTypeKind.Record
                or CSharpTypeKind.Struct or CSharpTypeKind.RecordStruct))
        {
            failures.Add($"the requirement of rule '{id}' has kind '{kind}', which must be Class, Interface, Record, Struct or RecordStruct");
        }

        CheckCondition(id, "is", requirement.Is, failures);
        CheckCondition(id, "isNot", requirement.IsNot, failures);

        if (requirement.Accessibility is { } accessibility
            && !accessibility.Equals("public", StringComparison.OrdinalIgnoreCase)
            && !accessibility.Equals("internal", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"the requirement of rule '{id}' has accessibility '{PackText.Show(accessibility)}', which must be public or internal");
        }

        if (requirement.Implements is { } implements
            && implements.Namespaces.Count == 0 && implements.Assemblies.Count == 0 && implements.Types.Count == 0)
        {
            failures.Add($"the implements of rule '{id}' needs at least one of namespaces, assemblies, types");
        }
    }

    // RequireRules ignores layers and members in a condition and checks nothing for an empty one.
    private static void CheckCondition(string id, string key, TargetSelector? condition, List<string> failures)
    {
        if (condition is null)
        {
            return;
        }

        if (condition.Layers.Count > 0 || condition.Members.Count > 0)
        {
            failures.Add($"the {key} of rule '{id}' has layers or members, which a condition does not take");
        }
        else if (condition is
                 {
                     Namespaces.Count: 0, Assemblies.Count: 0, Types.Count: 0, BaseTypes.Count: 0, Interfaces.Count: 0, Attributes.Count: 0,
                     Names.Count: 0, Kinds.Count: 0, Accessibility.Count: 0, AssignableTo.Count: 0,
                     Sealed: null, Abstract: null, Static: null, Nested: null,
                 })
        {
            failures.Add($"the {key} of rule '{id}' selects nothing; it needs at least one selector key other than layers and members");
        }
    }

    private static void CheckTargetLayers(string ruleId, TargetSelector target, HashSet<string> layerNames, List<string> failures)
    {
        foreach (var name in target.Layers)
        {
            if (!Contains(layerNames, name))
            {
                failures.Add($"rule '{PackText.Show(ruleId)}' refers to unknown layer '{PackText.Show(name)}'");
            }
        }
    }

    private static HashSet<string> CollectNames(IEnumerable<string> names, string path, string kind, List<string> failures)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var name in names)
        {
            // A missing name is a member-rule failure at load; skip it here rather than report it twice.
            if (!string.IsNullOrEmpty(name) && !seen.Add(name))
            {
                failures.Add($"{path}[{index}].Name: duplicate {kind} name '{PackText.Show(name)}'.");
            }

            index++;
        }

        return seen;
    }

    private static bool Contains(HashSet<string> names, string? name) => !string.IsNullOrEmpty(name) && names.Contains(name);
}
