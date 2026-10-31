namespace Ariadne.NET.Tests.Telemetry;

/// <summary>One measurement: the instrument's name, the value and its tags.</summary>
internal sealed record CapturedPoint(string Metric, double Value, KeyValuePair<string, object?>[] Tags)
{
    /// <summary>Gets the value of the tag <paramref name="key"/>, or <see langword="null"/> when the point has none.</summary>
    public object? Tag(string key) => Array.Find(Tags, tag => string.Equals(tag.Key, key, StringComparison.Ordinal)).Value;

    /// <summary>Gets whether the point carries the tag <paramref name="key"/>.</summary>
    public bool HasTag(string key) => Array.Exists(Tags, tag => string.Equals(tag.Key, key, StringComparison.Ordinal));
}
