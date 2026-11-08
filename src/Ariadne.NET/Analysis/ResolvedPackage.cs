namespace Ariadne.NET.Analysis;

/// <summary>One package a project resolves, with the route by which it got there.</summary>
/// <param name="Id">The package id, in the case the restore recorded.</param>
/// <param name="Version">The resolved version.</param>
/// <param name="IsDirect">Whether the project references the package itself.</param>
/// <param name="Path">The hops from the project to the package, in order: package ids, with the name of a referenced project for a project hop. The package itself is the last hop; it is empty only when nothing is known about the route.</param>
public sealed record ResolvedPackage(string Id, string Version, bool IsDirect, IReadOnlyList<string> Path);
