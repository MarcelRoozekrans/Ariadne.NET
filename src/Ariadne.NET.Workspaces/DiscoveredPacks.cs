namespace Ariadne.NET.Workspaces;

/// <summary>The pack files found in the nearest <c>.architecture</c> folder above a solution.</summary>
/// <param name="ProjectRoot">The folder that holds the <c>.architecture</c> folder.</param>
/// <param name="Files">Full paths of the <c>.yaml</c> and <c>.yml</c> files directly in it, sorted by file name with ordinal comparison.</param>
internal sealed record DiscoveredPacks(string ProjectRoot, IReadOnlyList<string> Files);
