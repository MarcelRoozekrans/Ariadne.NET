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

    /// <summary>A selector the type must match. Allows every selector key except <c>layers</c> and <c>members</c>.</summary>
    public TargetSelector? Is { get; init; }

    /// <summary>A selector the type must not match. Allows every selector key except <c>layers</c> and <c>members</c>.</summary>
    public TargetSelector? IsNot { get; init; }

    internal bool IsCodeBuildable(CSharpTypeKind? kind) => kind is not (CSharpTypeKind.Enum or CSharpTypeKind.Delegate);
}
