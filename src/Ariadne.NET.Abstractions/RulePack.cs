using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

[Validate]
public sealed class RulePack
{
    public required string Name { get; init; }
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = "";
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Extends { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<LayerDefinition> Layers { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<ComponentRoleDefinition> ComponentRoles { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<CodeFeatureRuleDefinition> CodeFeatureRules { get; init; } = [];
    [NotNull] public NamingConventions NamingConventions { get; init; } = new();

    internal bool HasNoNullEntries<T>(IList<T>? items) => NullEntries.None(items);
}
