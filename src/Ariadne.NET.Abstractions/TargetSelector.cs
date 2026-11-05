using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

/// <summary>Selects code by layer, namespace, assembly or type.</summary>
[Validate]
public sealed class TargetSelector
{
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Layers { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Namespaces { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Assemblies { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Types { get; init; } = [];

    internal bool HasNoNullEntries<T>(IList<T>? items) => NullEntries.None(items);
}
