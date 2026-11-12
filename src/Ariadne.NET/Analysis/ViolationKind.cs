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

    /// <summary>
    /// A rule or component role selects no type in the projects loaded, so it cannot fail and a clean report says nothing about
    /// it. It has no location and no target; the source is the rule id or role name. A rule or role marked <c>allowEmpty</c> is exempt.
    /// </summary>
    VacuousRule = 11,

    /// <summary>A layer covers no type in the projects loaded, so no rule that names it can fire. It has no location and no target; the source is the layer name.</summary>
    VacuousLayer = 12,

    /// <summary>
    /// A type a require rule selects fails one key of the rule's <c>is</c> condition, or matches one key of its <c>isNot</c>
    /// condition. There is one violation per key, located at the type.
    /// </summary>
    RequirementNotMet = 13,

    /// <summary>
    /// A slice depends on another slice in a cycle that a slices rule with <c>freeOfCycles</c> forbids. There is one violation
    /// per slice edge inside the cycle, located at the earliest reference from the one slice to the other; the source and
    /// target are that reference's types, and the reference count is the number of references between the two slices.
    /// </summary>
    SliceCycle = 14,

    /// <summary>
    /// A type in one slice depends on a type in another slice that a slices rule with <c>independent</c> keeps apart, and the
    /// rule's <c>allow</c> does not select the target. There is one violation per source and target type, located at the first reference.
    /// </summary>
    SliceDependency = 15,
}
