namespace Ariadne.NET.RuleEngine;

public sealed record DependencyViolation(
    string SourceLayer,
    string TargetLayer,
    string Message);
