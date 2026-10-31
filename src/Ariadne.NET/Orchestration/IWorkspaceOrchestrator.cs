using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Orchestration;

public interface IWorkspaceOrchestrator
{
    WorkspaceGenerateResult GenerateWorkspace(RulePack resolvedPack, WorkspaceGenerateRequest request);
}
