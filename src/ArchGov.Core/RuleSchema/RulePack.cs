namespace ArchGov.Core.RuleSchema;

public sealed class RulePack
{
    public required string Name { get; init; }
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = "";
    public List<string> Extends { get; init; } = [];
    public List<LayerDefinition> Layers { get; init; } = [];
    public List<ComponentRoleDefinition> ComponentRoles { get; init; } = [];
    public List<CodeFeatureRuleDefinition> CodeFeatureRules { get; init; } = [];
    public NamingConventions NamingConventions { get; init; } = new();
}
