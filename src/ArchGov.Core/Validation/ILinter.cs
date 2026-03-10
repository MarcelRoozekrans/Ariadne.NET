using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.Validation;

public interface ILinter
{
    LintResult Lint(RulePack resolvedPack, string sourceDirectory);
}
