using Ariadne.NET.Abstractions;

namespace Ariadne.NET.RuleEngine;

public interface ILayerInferenceEngine
{
    LayerInferenceResult Infer(RulePack resolvedPack, string roleName);
}
