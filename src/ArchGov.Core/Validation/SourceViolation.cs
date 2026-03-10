namespace ArchGov.Core.Validation;

public sealed record SourceViolation
{
    public required string FilePath { get; init; }
    public required string ViolationType { get; init; }
    public required string Message { get; init; }
}
