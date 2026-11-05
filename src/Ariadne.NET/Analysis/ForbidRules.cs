using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Analysis;

/// <summary>The <c>forbid</c> rules of a pack, evaluated on the type graph.</summary>
internal static class ForbidRules
{
    /// <summary>Compiles the rules once, for a run that checks several graphs.</summary>
    public static DependencyRuleChecker Compile(LayerMap layers, RulePack pack) =>
        DependencyRuleChecker.Compile(layers, pack, RuleKind.Forbid);

    /// <summary>Returns one violation per rule, source type and target type with a reference count, located at the first reference.</summary>
    public static IReadOnlyList<ArchitectureViolation> Check(TypeGraph graph, LayerMap layers, RulePack pack) =>
        Compile(layers, pack).Check(graph);
}
