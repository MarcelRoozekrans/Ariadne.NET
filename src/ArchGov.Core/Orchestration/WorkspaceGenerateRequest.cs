namespace ArchGov.Core.Orchestration;

public sealed record WorkspaceGenerateRequest
{
    public required string SolutionName { get; init; }
    public required IReadOnlyList<string> Features { get; init; }
}
