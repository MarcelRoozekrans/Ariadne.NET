using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

/// <summary>A layer entry in a child pack that only changes the layer's selectors.</summary>
[Validate]
public sealed class LayerRetarget
{
    [NotEmpty] public required string Name { get; init; }
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Namespaces { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Assemblies { get; init; } = [];

    internal bool HasNoNullEntries<T>(IList<T>? items) => NullEntries.None(items);
}
