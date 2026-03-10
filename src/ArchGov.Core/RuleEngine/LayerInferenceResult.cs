namespace ArchGov.Core.RuleEngine;

public sealed record LayerInferenceResult
{
    public required LayerInferenceStatus Status { get; init; }
    public string? Layer { get; init; }
    public string? RoleName { get; init; }
    public IReadOnlyList<string>? AmbiguousOptions { get; init; }
    public string? Message { get; init; }
}

