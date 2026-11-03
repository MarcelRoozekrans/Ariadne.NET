namespace Ariadne.NET.Abstractions;

/// <summary>Classifies an <see cref="AriadneError"/>.</summary>
/// <remarks>
/// The values are numbered from 1 and 0 is deliberately undefined. A <c>default</c>
/// <c>Result</c> is a failure whose error is <c>default(AriadneError)</c>, so its code reads 0
/// and is recognisable as never having been set, rather than passing for a
/// <see cref="NotFound"/> failure.
/// </remarks>
public enum AriadneErrorCode
{
    /// <summary>The requested pack, file or import does not exist.</summary>
    NotFound = 1,

    /// <summary>The input is malformed or breaks a rule, such as invalid YAML or an unknown reference.</summary>
    Invalid = 2,

    /// <summary>A backing dependency, such as git or the file system, failed.</summary>
    Unavailable = 3,
}
