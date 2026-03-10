namespace ArchGov.Core.CodeGeneration;

public sealed record CodeGenerationRequest
{
    public required string SolutionName { get; init; }
    public required string Feature { get; init; }
    public required string ComponentName { get; init; }
    public required string RoleName { get; init; }
}
