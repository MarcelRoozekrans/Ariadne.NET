namespace Ariadne.NET.Analysis;

/// <summary>The kind of rule a violation breaks.</summary>
public enum ViolationKind
{
    /// <summary>A type depends on a type its rule does not allow.</summary>
    DependencyNotAllowed = 1,

    /// <summary>A type is not of the kind the rule requires.</summary>
    TypeKindMismatch = 2,

    /// <summary>A type does not have the accessibility the rule requires.</summary>
    AccessibilityMismatch = 3,

    /// <summary>A type does not implement an interface the rule requires.</summary>
    MissingInterface = 4,
}
