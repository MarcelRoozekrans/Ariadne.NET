using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

/// <summary>
/// The slices a <see cref="RuleKind.Slices"/> rule cuts the code into, and the condition they must meet.
/// Exactly one of <see cref="FreeOfCycles"/> and <see cref="Independent"/> is <see langword="true"/>.
/// </summary>
[Validate]
public sealed class SliceDefinition
{
    /// <summary>
    /// Dotted namespace patterns, each with exactly one capture: <c>(*)</c> captures exactly one segment and
    /// <c>(**)</c> one or more. The capture is a whole segment and never matches zero segments; the rest of the
    /// pattern is a dotted glob. Within a pattern the earliest capture start wins, then for <c>(**)</c> the shortest
    /// capture that lets the rest match. Across patterns the first one in list order that captures wins.
    /// </summary>
    [NotNull, NotEmpty, Must(nameof(HasNoNullEntries), Message = "{PropertyName} must not contain null entries.")]
    public IList<string> Matching { get; init; } = [];

    /// <summary>When <see langword="true"/>, the dependencies between slices must not form a cycle.</summary>
    public bool FreeOfCycles { get; init; }

    /// <summary>When <see langword="true"/>, no slice may depend on another slice, except on code matching <see cref="Allow"/>.</summary>
    [Must(nameof(ExactlyOneCondition), Message = "Exactly one of freeOfCycles and independent must be true.")]
    public bool Independent { get; init; }

    /// <summary>
    /// Targets in another slice that a slice may depend on anyway. Takes the keys of a <c>forbid</c> rule's <c>to</c>
    /// selector, <c>layers</c> included and <c>members</c> excluded.
    /// </summary>
    public TargetSelector? Allow { get; init; }

    internal bool HasNoNullEntries(IList<string>? items) => NullEntries.None(items);

    internal bool ExactlyOneCondition(bool independent) => FreeOfCycles != independent;
}
