using Ariadne.NET.CodeGeneration;
using Ariadne.NET.RuleEngine;

namespace Ariadne.NET.Orchestration;

public sealed record GenerateResult
{
    public required bool Success { get; init; }
    public IReadOnlyList<CodeGenerationResult> GeneratedFiles { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<DependencyViolation> DependencyViolations { get; init; } = [];
}
