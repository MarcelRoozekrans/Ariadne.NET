using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleEngine;

public sealed record MergeResult
{
    public required RulePack MergedPack { get; init; }
    public required IReadOnlyList<MergeWarning> Warnings { get; init; }
}
