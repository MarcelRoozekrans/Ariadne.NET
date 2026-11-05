using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

/// <summary>An exemption from a rule, with the reason it exists.</summary>
[Validate]
public sealed class RuleException
{
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Namespaces { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Assemblies { get; init; } = [];
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<string> Types { get; init; } = [];
    [NotEmpty] public required string Because { get; init; }

    internal bool HasNoNullEntries<T>(IList<T>? items) => NullEntries.None(items);
}
