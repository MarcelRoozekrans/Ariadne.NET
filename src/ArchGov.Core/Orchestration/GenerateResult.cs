using ArchGov.Core.CodeGeneration;
using ArchGov.Core.RuleEngine;

namespace ArchGov.Core.Orchestration;

public sealed record GenerateResult
{
    public required bool Success { get; init; }
    public IReadOnlyList<CodeGenerationResult> GeneratedFiles { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<DependencyViolation> DependencyViolations { get; init; } = [];
}
