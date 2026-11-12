using ZeroAlloc.Validation;

namespace Ariadne.NET.Abstractions;

/// <summary>What code selected by a <c>require</c> rule must satisfy. Unset members are not checked.</summary>
[Validate]
public sealed class RuleRequirement
{
    /// <summary>The layer the type must belong to.</summary>
    public string? Layer { get; init; }

    /// <summary>The kind the type must have.</summary>
    [Must(nameof(IsCodeBuildable), Message = "{PropertyName} must be Class, Interface, Record, Struct or RecordStruct.")]
    public CSharpTypeKind? Kind { get; init; }

    /// <summary>The accessibility the type must have.</summary>
    public string? Accessibility { get; init; }

    /// <summary>A selector for an interface the type must implement, including interfaces it gets through a base class.</summary>
    public TargetSelector? Implements { get; init; }

    /// <summary>
    /// A selector the type must match, checked key by key: each key the type does not match is its own violation, and the
    /// values within one key stay alternatives. Allows every selector key except <c>layers</c> and <c>members</c>.
    /// </summary>
    public TargetSelector? Is { get; init; }

    /// <summary>
    /// Keys the type must match none of, checked key by key: each key the type matches is its own violation, even when the
    /// type does not match the selector as a whole, and the values within one key stay alternatives. Allows every selector
    /// key except <c>layers</c> and <c>members</c>.
    /// </summary>
    public TargetSelector? IsNot { get; init; }

    internal bool IsCodeBuildable(CSharpTypeKind? kind) => kind is not (CSharpTypeKind.Enum or CSharpTypeKind.Delegate);
}
