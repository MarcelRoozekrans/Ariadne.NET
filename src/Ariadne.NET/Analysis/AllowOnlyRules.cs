using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Analysis;

/// <summary>The <c>allowOnly</c> rules of a pack, evaluated on the type graph.</summary>
internal static class AllowOnlyRules
{
    /// <summary>Compiles the rules once, for a run that checks several graphs.</summary>
    /// <param name="layers">The layer map of the pack.</param>
    /// <param name="pack">The resolved pack.</param>
    /// <param name="dependencies">The run's dependency index, or <see langword="null"/> when the pack uses no dependency key.</param>
    /// <returns>The compiled rules.</returns>
    public static DependencyRuleChecker Compile(LayerMap layers, RulePack pack, DependencyIndex? dependencies = null) =>
        DependencyRuleChecker.Compile(layers, pack, RuleKind.AllowOnly, dependencies);

    /// <summary>Returns one violation per rule, source type and target type with a reference count, located at the first reference.</summary>
    public static IReadOnlyList<ArchitectureViolation> Check(TypeGraph graph, LayerMap layers, RulePack pack) =>
        Compile(layers, pack).Check(graph);
}
