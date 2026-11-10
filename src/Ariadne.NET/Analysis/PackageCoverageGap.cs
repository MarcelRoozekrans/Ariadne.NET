namespace Ariadne.NET.Analysis;

/// <summary>A project that a <c>bannedPackages</c> rule covers while the project's packages are not known.</summary>
/// <param name="RuleId">The id of the rule that covers the project.</param>
/// <param name="ProjectName">The name of the project.</param>
public readonly record struct PackageCoverageGap(string RuleId, string ProjectName);
