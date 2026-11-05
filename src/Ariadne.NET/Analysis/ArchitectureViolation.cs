namespace Ariadne.NET.Analysis;

/// <summary>One broken architecture rule.</summary>
public sealed record ArchitectureViolation
{
    /// <summary>The id of the rule that was broken.</summary>
    public required string RuleId { get; init; }

    /// <summary>The kind of rule that was broken.</summary>
    public required ViolationKind Kind { get; init; }

    /// <summary>The type the violation was found in.</summary>
    public required string SourceType { get; init; }

    /// <summary>A human-readable description of the violation.</summary>
    public required string Message { get; init; }

    /// <summary>The rationale the rule gives, empty when it has none.</summary>
    public string Because { get; init; } = "";

    /// <summary>The type depended on, when the violation concerns a dependency.</summary>
    public string? Target { get; init; }

    /// <summary>Where the first offending reference sits, when known.</summary>
    public SourceLocation? Location { get; init; }

    /// <summary>How many references were merged into this violation.</summary>
    public int ReferenceCount { get; init; } = 1;

    /// <summary>The target frameworks the violation was found in.</summary>
    public IReadOnlyList<string> TargetFrameworks { get; init; } = [];
}
