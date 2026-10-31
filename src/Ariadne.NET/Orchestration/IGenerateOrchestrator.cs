using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Orchestration;

public interface IGenerateOrchestrator
{
    GenerateResult Generate(RulePack resolvedPack, GenerateRequest request);
}
