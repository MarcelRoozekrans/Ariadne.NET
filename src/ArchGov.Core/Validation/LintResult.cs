namespace ArchGov.Core.Validation;

public sealed record LintResult
{
    public required bool IsClean { get; init; }
    public required IReadOnlyList<LintViolation> Violations { get; init; }
    public required int FilesScanned { get; init; }
}
