using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

[Validate]
public sealed class RulePack
{
    public required string Name { get; init; }
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = "";
    [NotNull, Must(nameof(HasNoNullEntries))] public IList<string> Extends { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries))] public IList<LayerDefinition> Layers { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries))] public IList<ComponentRoleDefinition> ComponentRoles { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries))] public IList<CodeFeatureRuleDefinition> CodeFeatureRules { get; init; } = [];
    [NotNull] public NamingConventions NamingConventions { get; init; } = new();

    internal bool HasNoNullEntries<T>(IList<T>? items) => items is null || !items.Any(static i => i is null);
}
