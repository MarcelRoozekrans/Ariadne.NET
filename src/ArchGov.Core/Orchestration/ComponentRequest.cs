namespace ArchGov.Core.Orchestration;

public sealed record ComponentRequest
{
    public required string RoleName { get; init; }
    public required string Name { get; init; }
}
