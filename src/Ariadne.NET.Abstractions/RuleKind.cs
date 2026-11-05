namespace Ariadne.NET.Abstractions;

/// <summary>The kind of dependency rule a <see cref="RuleDefinition"/> expresses.</summary>
public enum RuleKind
{
    /// <summary>Code matching From must not depend on code matching To.</summary>
    Forbid = 1,

    /// <summary>Code matching From may depend only on code matching To.</summary>
    AllowOnly = 2,
}
