using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

/// <summary>A rule entry in a child pack that only adds exceptions to an inherited rule.</summary>
[Validate]
public sealed class RuleAmendment
{
    [NotEmpty] public required string Id { get; init; }
    [NotNull, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")] public IList<RuleException> Exceptions { get; init; } = [];

    internal bool HasNoNullEntries<T>(IList<T>? items) => NullEntries.None(items);
}
