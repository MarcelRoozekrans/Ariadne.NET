using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleEngine;

public interface IRulePackMerger
{
    RulePack Merge(IReadOnlyList<RulePack> packs);
    MergeResult MergeWithDiagnostics(IReadOnlyList<RulePack> packs);
}
