namespace Ariadne.NET.Abstractions;

/// <summary>The kind of dependency rule a <see cref="RuleDefinition"/> expresses.</summary>
public enum RuleKind
{
    /// <summary>Code matching From must not depend on code matching To.</summary>
    Forbid = 1,

    /// <summary>Code matching To may be depended on only by code matching From, or by code matching To itself.</summary>
    AllowOnly = 2,

    /// <summary>Code matching From must satisfy the <see cref="RuleDefinition.Require"/> requirement.</summary>
    Require = 3,

    /// <summary>No project in From may reference a package matching <see cref="RuleDefinition.Packages"/>.</summary>
    BannedPackages = 4,

    /// <summary>Types matching From must not share a simple name with types matching <see cref="RuleDefinition.Against"/>.</summary>
    UniqueNames = 5,
}
