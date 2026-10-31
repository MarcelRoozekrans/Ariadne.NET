using Ariadne.NET.RuleSchema;

namespace Ariadne.NET.RuleEngine;

public sealed record MergeResult
{
    public required RulePack MergedPack { get; init; }
    public required IReadOnlyList<MergeWarning> Warnings { get; init; }
}
