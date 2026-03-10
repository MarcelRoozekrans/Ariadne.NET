namespace ArchGov.Core.Orchestration;

public sealed record GenerateRequest
{
    public required string SolutionName { get; init; }
    public required string Feature { get; init; }
    public required IReadOnlyList<ComponentRequest> Components { get; init; }
}
