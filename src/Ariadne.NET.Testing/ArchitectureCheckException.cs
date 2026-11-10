using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Testing;

/// <summary>
/// Thrown when an architecture check could not run: no pack was found, a pack is invalid, no solution was found or the solution
/// could not be loaded. <see cref="ArchitectureViolationException"/> derives from it, so test code can catch one type for every
/// failed check.
/// </summary>
public class ArchitectureCheckException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public ArchitectureCheckException()
    {
    }

    /// <summary>Creates the exception with the given message.</summary>
    /// <param name="message">What went wrong.</param>
    public ArchitectureCheckException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with the given message and the exception that caused it.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public ArchitectureCheckException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for an error the check returned: its message, then its detail on the next line when it has one.</summary>
    /// <param name="error">The error the check returned.</param>
    internal ArchitectureCheckException(AriadneError error)
        : base(error.Detail is null ? error.Message : error.Message + Environment.NewLine + error.Detail)
    {
    }
}
