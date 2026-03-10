namespace ArchGov.Core.Sync;

public sealed class SyncResult
{
    public required string ImportName { get; init; }
    public string? ResolvedVersion { get; init; }
    public bool WasCached { get; init; }
    public string? CachePath { get; init; }
    public bool Success { get; init; } = true;
    public string? Error { get; init; }
}
