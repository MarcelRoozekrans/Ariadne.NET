using Ariadne.NET.Abstractions;

using ZeroAlloc.Results;

namespace Ariadne.NET.RuleEngine;

public interface IRulePackMerger
{
    /// <summary>
    /// Merges packs in list order, which is the parents in <c>extends</c> order and then the child. A conflict, an
    /// unknown retarget or amendment target, or an amendment of a locked rule fails with every problem listed.
    /// </summary>
    Result<RulePack, AriadneError> Merge(IReadOnlyList<RulePack> packs);
}
