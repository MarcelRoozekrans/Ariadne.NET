namespace Ariadne.NET.Orchestration;

public sealed record WorkspaceGenerateResult
{
    public required IReadOnlyList<string> ProjectPaths { get; init; }
    public required IReadOnlyList<string> Directories { get; init; }
}
