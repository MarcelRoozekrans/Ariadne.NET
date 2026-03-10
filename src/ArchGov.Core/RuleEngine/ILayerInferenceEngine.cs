using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleEngine;

public interface ILayerInferenceEngine
{
    LayerInferenceResult Infer(RulePack resolvedPack, string roleName);
}
