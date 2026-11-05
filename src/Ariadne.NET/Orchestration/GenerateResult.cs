using Ariadne.NET.CodeGeneration;

namespace Ariadne.NET.Orchestration;

public sealed record GenerateResult
{
    public required bool Success { get; init; }
    public IReadOnlyList<CodeGenerationResult> GeneratedFiles { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}
