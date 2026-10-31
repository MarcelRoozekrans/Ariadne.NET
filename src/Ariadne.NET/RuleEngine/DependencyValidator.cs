using Ariadne.NET.Abstractions;

using ZInject;

namespace Ariadne.NET.RuleEngine;

[Singleton]
public sealed class DependencyValidator : IDependencyValidator
{
    public IReadOnlyList<DependencyViolation> Validate(RulePack resolvedPack, string sourceLayer, IReadOnlyList<string> dependsOnLayers)
    {
        ArgumentNullException.ThrowIfNull(resolvedPack);
        ArgumentNullException.ThrowIfNull(sourceLayer);
        ArgumentNullException.ThrowIfNull(dependsOnLayers);

        var layerDefinition = resolvedPack.Layers
            .FirstOrDefault(l => string.Equals(l.Name, sourceLayer, StringComparison.OrdinalIgnoreCase));

        if (layerDefinition is null)
        {
            return [new DependencyViolation(sourceLayer, "", $"Unknown layer: {sourceLayer}")];
        }

        var allowedSet = new HashSet<string>(layerDefinition.AllowedDependencies, StringComparer.OrdinalIgnoreCase);
        var violations = new List<DependencyViolation>();

        foreach (var targetLayer in dependsOnLayers)
        {
            if (!allowedSet.Contains(targetLayer))
            {
                violations.Add(new DependencyViolation(
                    sourceLayer,
                    targetLayer,
                    $"Layer '{sourceLayer}' is not allowed to depend on '{targetLayer}'"));
            }
        }

        return violations;
    }
}
