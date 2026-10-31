namespace Ariadne.NET.Validation;

public sealed record LintViolation
{
    public required string FilePath { get; init; }
    public required string Category { get; init; }
    public required string Message { get; init; }
}
