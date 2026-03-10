namespace ArchGov.Host.Api.Models;

public sealed record ApiWorkspaceRequest
{
    public required string SolutionName { get; init; }
    public string? Template { get; init; }
    public required IReadOnlyList<string> Features { get; init; }
}
