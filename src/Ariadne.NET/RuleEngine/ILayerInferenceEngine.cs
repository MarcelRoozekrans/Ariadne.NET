using Ariadne.NET.RuleSchema;

namespace Ariadne.NET.RuleEngine;

public interface ILayerInferenceEngine
{
    LayerInferenceResult Infer(RulePack resolvedPack, string roleName);
}
