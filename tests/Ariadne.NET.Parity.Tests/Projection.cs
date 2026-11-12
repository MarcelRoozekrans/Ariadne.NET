namespace Ariadne.NET.Parity.Tests;

/// <summary>What a parity case compares between the two tools.</summary>
public enum Projection
{
    /// <summary>The canonical names of the failing types.</summary>
    Types,

    /// <summary>The full namespaces of the slices involved in a failure.</summary>
    Slices,
}
