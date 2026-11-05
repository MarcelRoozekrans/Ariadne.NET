using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

[Validate]
public sealed class LayerDefinition
{
    [NotEmpty] public required string Name { get; init; }
    public string Id { get; init; } = "";
    public string Because { get; init; } = "";
    public string Description { get; init; } = "";
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> AllowedDependencies { get; init; } = [];

    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Namespaces { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Assemblies { get; init; } = [];

    internal bool HasNoNullEntries<T>(IList<T>? items) => NullEntries.None(items);
}
