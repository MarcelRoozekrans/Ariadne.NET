using System.Text.Json.Serialization;

namespace Ariadne.NET.Sync;

public sealed class GitInfo
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("resolvedTag")]
    public required string ResolvedTag { get; init; }

    [JsonPropertyName("commitSha")]
    public required string CommitSha { get; init; }

    [JsonPropertyName("syncedAt")]
    public required DateTimeOffset SyncedAt { get; init; }
}
