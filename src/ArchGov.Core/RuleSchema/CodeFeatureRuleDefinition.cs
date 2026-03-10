namespace ArchGov.Core.RuleSchema;

public sealed class CodeFeatureRuleDefinition
{
    public required string Id { get; init; }
    public required string Description { get; init; }
    public Dictionary<string, object> AppliesTo { get; init; } = [];
    public bool Locked { get; init; }
}
