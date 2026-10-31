using Ariadne.NET.RuleSchema;

namespace Ariadne.NET.Orchestration;

public interface IWorkspaceOrchestrator
{
    WorkspaceGenerateResult GenerateWorkspace(RulePack resolvedPack, WorkspaceGenerateRequest request);
}
