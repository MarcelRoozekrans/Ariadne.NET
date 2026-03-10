using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleEngine;

public interface IDependencyValidator
{
    IReadOnlyList<DependencyViolation> Validate(RulePack resolvedPack, string sourceLayer, IReadOnlyList<string> dependsOnLayers);
}
