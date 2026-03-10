namespace ArchGov.Host.Api.Models;

public sealed record ApiGenerateRequest
{
    public required string SolutionName { get; init; }
    public string? Template { get; init; }
    public IList<string>? Features { get; init; }
    public required IList<ApiComponentRequest> Components { get; init; }
}
