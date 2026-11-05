using Ariadne.NET.Abstractions;

using ZeroAlloc.Inject;

namespace Ariadne.NET.Orchestration;

[Singleton]
public sealed class WorkspaceOrchestrator : IWorkspaceOrchestrator
{
    public WorkspaceGenerateResult GenerateWorkspace(RulePack resolvedPack, WorkspaceGenerateRequest request)
    {
        ArgumentNullException.ThrowIfNull(resolvedPack);
        ArgumentNullException.ThrowIfNull(request);

        var directories = new List<string>();
        var projectPaths = new List<string>();

        foreach (var feature in request.Features)
        {
            foreach (var layer in resolvedPack.Layers)
            {
                var projectName = resolvedPack.NamingConventions.Namespace
                    .Replace("{SolutionName}", request.SolutionName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Feature}", feature, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Layer}", layer.Name, StringComparison.OrdinalIgnoreCase);

                var dirPath = $"src/{projectName}";
                directories.Add(dirPath);
                projectPaths.Add($"{dirPath}/{projectName}.csproj");
            }
        }

        return new WorkspaceGenerateResult
        {
            ProjectPaths = projectPaths,
            Directories = directories,
        };
    }
}
