using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

[Validate]
public sealed class RulePack
{
    public required string Name { get; init; }
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = "";
    public IList<string> Extends { get; init; } = [];
    public IList<LayerDefinition> Layers { get; init; } = [];
    public IList<ComponentRoleDefinition> ComponentRoles { get; init; } = [];
    public IList<CodeFeatureRuleDefinition> CodeFeatureRules { get; init; } = [];
    public NamingConventions NamingConventions { get; init; } = new();
}
