using Ariadne.NET.Abstractions;

namespace Ariadne.NET.RuleEngine;

public interface IRulePackMerger
{
    RulePack Merge(IReadOnlyList<RulePack> packs);
    MergeResult MergeWithDiagnostics(IReadOnlyList<RulePack> packs);
}
