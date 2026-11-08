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

    /// <summary>A type depends on a type that a forbid rule bars it from.</summary>
    ForbiddenDependency = 5,

    /// <summary>A type depends on a type that no allow-only rule permits.</summary>
    DependencyNotPermitted = 6,

    /// <summary>A type is covered by more than one layer.</summary>
    LayerOverlap = 7,

    /// <summary>A type a require rule selects is not in the layer the rule requires.</summary>
    WrongLayer = 8,

    /// <summary>A project references a package that a bannedPackages rule bars it from.</summary>
    BannedPackage = 9,

    /// <summary>
    /// A type keeps the simple name of another type that its uniqueNames rule sets it apart from. The target is the other
    /// type's full name, without its assembly; when the two full names are equal, the message names both assemblies.
    /// </summary>
    NameCollision = 10,
}
