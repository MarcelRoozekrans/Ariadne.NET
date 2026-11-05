using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

[Validate]
public sealed class RulePack
{
    public required string Name { get; init; }
    /// <summary>
    /// The schema version, always 2. The reader accepts only <c>schema: 2</c> and the merger sets 2, so nothing else reads
    /// this value; it records which schema the pack was written for.
    /// </summary>
    public int Schema { get; init; } = 2;
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = "";
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Extends { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<LayerDefinition> Layers { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<ComponentRoleDefinition> ComponentRoles { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<RuleDefinition> Rules { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<LayerRetarget> LayerRetargets { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<RuleAmendment> RuleAmendments { get; init; } = [];
    [NotNull] public NamingConventions NamingConventions { get; init; } = new();

    internal bool HasNoNullEntries<T>(IList<T>? items) => NullEntries.None(items);
}
