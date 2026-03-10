namespace ArchGov.Host.Api.Models;

public sealed record ApiComponentRequest
{
    public required string ComponentRole { get; init; }
    public required string Name { get; init; }
}
