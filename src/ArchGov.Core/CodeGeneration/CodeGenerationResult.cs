namespace ArchGov.Core.CodeGeneration;

public sealed record CodeGenerationResult
{
    public required string FilePath { get; init; }
    public required string Code { get; init; }
    public required string Namespace { get; init; }
    public required string TypeName { get; init; }
}
