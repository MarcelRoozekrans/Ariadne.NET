namespace ArchGov.Core.RuleSchema;

public sealed class LayerDefinition
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public List<string> AllowedDependencies { get; init; } = [];
}
