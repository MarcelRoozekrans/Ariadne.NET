namespace ArchGov.Host.Api.Models;

public sealed record ApiValidateRequest
{
    public required string SourceDirectory { get; init; }
    public string? Template { get; init; }
}
