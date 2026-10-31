using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

[Validate]
public sealed class LayerDefinition
{
    [NotEmpty] public required string Name { get; init; }
    public string Description { get; init; } = "";
    [NotNull, Must(nameof(HasNoNullEntries))] public IList<string> AllowedDependencies { get; init; } = [];

    internal bool HasNoNullEntries<T>(IList<T>? items) => items is null || !items.Any(static i => i is null);
}
