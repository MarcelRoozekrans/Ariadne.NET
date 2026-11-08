namespace Ariadne.NET.Hooks;

/// <summary>What the import cache says: warnings for stale or missing entries and the cached versions.</summary>
/// <param name="Warnings">One warning per import that needs <c>ariadne sync</c> or a fix.</param>
/// <param name="Versions">The <c>name@resolvedTag</c> of every cached import.</param>
public sealed record CacheStatus(IReadOnlyList<string> Warnings, IReadOnlyList<string> Versions);
