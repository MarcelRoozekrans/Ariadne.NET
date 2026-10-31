namespace Ariadne.NET.Abstractions;

public sealed class CodeFeatureRuleDefinition
{
    public required string Id { get; init; }
    public required string Description { get; init; }
    public IDictionary<string, object> AppliesTo { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);
    public bool Locked { get; init; }
}
