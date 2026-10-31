namespace Ariadne.NET.Validation;

public sealed record SourceValidationResult
{
    public required bool IsValid { get; init; }
    public required IReadOnlyList<SourceViolation> Violations { get; init; }
    public required int FilesScanned { get; init; }
}
