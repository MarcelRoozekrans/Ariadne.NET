using Ariadne.NET.Abstractions;

namespace Ariadne.NET.RuleEngine;

public sealed record MergeResult
{
    public required RulePack MergedPack { get; init; }
    public required IReadOnlyList<MergeWarning> Warnings { get; init; }
}
