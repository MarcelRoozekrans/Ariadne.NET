namespace Ariadne.NET.Analysis;

/// <summary>A project that was analysed.</summary>
/// <param name="Name">The project name.</param>
/// <param name="TargetFrameworks">The target frameworks that were analysed.</param>
public sealed record CheckedProject(string Name, IReadOnlyList<string> TargetFrameworks);
