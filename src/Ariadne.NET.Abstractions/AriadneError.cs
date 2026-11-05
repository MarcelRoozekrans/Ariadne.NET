namespace Ariadne.NET.Abstractions;

/// <summary>Describes why an Ariadne operation failed.</summary>
/// <param name="Code">The error classification.</param>
/// <param name="Message">A human-readable message.</param>
/// <param name="Detail">Optional extra detail, such as git's stderr output.</param>
public readonly record struct AriadneError(AriadneErrorCode Code, string Message, string? Detail = null);
