using Ariadne.NET.Abstractions;

namespace Ariadne.NET.RuleEngine;

public interface IDependencyValidator
{
    IReadOnlyList<DependencyViolation> Validate(RulePack resolvedPack, string sourceLayer, IReadOnlyList<string> dependsOnLayers);
}
