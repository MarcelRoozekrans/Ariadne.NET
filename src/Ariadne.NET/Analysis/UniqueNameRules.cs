using System.Globalization;
using System.Runtime.InteropServices;
using Ariadne.NET.Abstractions;
using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Analysis;

/// <summary>The <c>uniqueNames</c> rules of a pack, evaluated over the source types of every project of a run.</summary>
internal static class UniqueNameRules
{
    /// <summary>
    /// Returns one violation per selected <c>for</c> type and colliding <c>against</c> type, located at the <c>for</c> type.
    /// Two types collide when their simple names are equal, compared case-sensitively with the generic arity and without the
    /// namespace; a nested type uses its own name. A type in both selections never collides with itself. A type that several
    /// graphs hold, as a project does for each of its target frameworks, counts once, as the first variant that no exception
    /// excuses. The violation's target is the colliding
    /// type's full name without its assembly; when both full names are equal the message names the two assemblies.
    /// </summary>
    public static IReadOnlyList<ArchitectureViolation> Check(LayerMap layers, RulePack pack, IReadOnlyList<TypeGraph> graphs)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(graphs);

        var violations = new List<ArchitectureViolation>();
        foreach (var rule in pack.Rules)
        {
            if (rule.Kind == RuleKind.UniqueNames)
            {
                CheckRule(rule, layers, graphs, violations);
            }
        }

        return violations;
    }

    private static void CheckRule(RuleDefinition rule, LayerMap layers, IReadOnlyList<TypeGraph> graphs, List<ArchitectureViolation> violations)
    {
        var from = TargetMatcher.From(rule.From, layers);
        var against = TargetMatcher.From(rule.Against, layers);
        var exceptions = new TypeSelector[rule.Exceptions.Count];
        for (var i = 0; i < exceptions.Length; i++)
        {
            exceptions[i] = TypeSelector.From(rule.Exceptions[i]);
        }

        var (againstByName, forTypes) = Collect(from, against, exceptions, layers, graphs);

        foreach (ref readonly var forType in CollectionsMarshal.AsSpan(forTypes))
        {
            if (!againstByName.TryGetValue(forType.Name, out var colliding))
            {
                continue;
            }

            foreach (ref readonly var other in CollectionsMarshal.AsSpan(colliding))
            {
                if (other == forType.Id)
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation
                {
                    RuleId = rule.Id,
                    Kind = ViolationKind.NameCollision,
                    SourceType = forType.Display,
                    Target = other.Display,
                    Location = forType.Location,
                    Because = rule.Because,
                    Message = Message(rule.Id, forType.Id, in other),
                });
            }
        }
    }

    private static (Dictionary<string, List<TypeId>> AgainstByName, List<ForType> ForTypes) Collect(
        TargetMatcher from, TargetMatcher against, TypeSelector[] exceptions, LayerMap layers, IReadOnlyList<TypeGraph> graphs)
    {
        // Each side counts a type once, but a for type only once it passes the exceptions: a target framework variant that
        // an exception excuses, such as one declared sealed under a condition, must not hide a variant it does not excuse.
        var seenAgainst = new HashSet<TypeId>();
        var seenFor = new HashSet<TypeId>();
        var againstByName = new Dictionary<string, List<TypeId>>(StringComparer.Ordinal);
        var forTypes = new List<ForType>();
        foreach (var graph in graphs)
        {
            foreach (var type in graph.SourceTypes)
            {
                var id = new TypeId(type.ContainingAssembly?.Name ?? string.Empty, type.ToDisplayString());
                var name = NameOf(type);
                if (against.Matches(type) && seenAgainst.Add(id))
                {
                    if (!againstByName.TryGetValue(name, out var list))
                    {
                        againstByName.Add(name, list = []);
                    }

                    list.Add(id);
                }

                if (from.Matches(type) && !IsExcepted(exceptions, type, layers.Names) && seenFor.Add(id))
                {
                    forTypes.Add(new ForType(name, id, graph.SourceTypeLocations[type]));
                }
            }
        }

        return (againstByName, forTypes);
    }

    // Two types with the same full name in different assemblies read the same, so the message then names the assemblies.
    private static string Message(string ruleId, in TypeId type, in TypeId other) =>
        string.Equals(type.Display, other.Display, StringComparison.Ordinal)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Type '{type.Display}' in assembly '{type.Assembly}' is selected by rule '{ruleId}' and shares its name with '{other.Display}' in assembly '{other.Assembly}'")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Type '{type.Display}' is selected by rule '{ruleId}' and shares its name with '{other.Display}'");

    // Case-sensitive and without the namespace; the arity keeps Result and Result<T> apart.
    private static string NameOf(INamedTypeSymbol type) =>
        type.Arity == 0 ? type.Name : string.Create(CultureInfo.InvariantCulture, $"{type.Name}`{type.Arity}");

    private static bool IsExcepted(TypeSelector[] exceptions, INamedTypeSymbol type, TypeNames names)
    {
        foreach (var exception in exceptions)
        {
            if (exception.Matches(type, names))
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct ForType(string Name, TypeId Id, SourceLocation Location)
    {
        public string Display => Id.Display;
    }

    // The assembly name is the project identity: the graphs of one project's target frameworks share it, while distinct
    // projects that declare the same full name stay apart.
    private readonly record struct TypeId(string Assembly, string Display);
}
