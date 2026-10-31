using Ariadne.NET.RuleSchema;

namespace Ariadne.NET.Orchestration;

public interface IGenerateOrchestrator
{
    GenerateResult Generate(RulePack resolvedPack, GenerateRequest request);
}
