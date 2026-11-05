namespace Ariadne.NET.Analysis;

/// <summary>A project that was not analysed.</summary>
/// <param name="Name">The project name.</param>
/// <param name="Reason">Why the project was skipped.</param>
public sealed record SkippedProject(string Name, string Reason);
