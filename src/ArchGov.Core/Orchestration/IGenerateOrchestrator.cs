using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.Orchestration;

public interface IGenerateOrchestrator
{
    GenerateResult Generate(RulePack resolvedPack, GenerateRequest request);
}
