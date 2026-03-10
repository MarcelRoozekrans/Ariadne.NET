using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.Orchestration;

public interface IWorkspaceOrchestrator
{
    WorkspaceGenerateResult GenerateWorkspace(RulePack resolvedPack, WorkspaceGenerateRequest request);
}
