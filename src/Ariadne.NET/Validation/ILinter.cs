using Ariadne.NET.RuleSchema;

namespace Ariadne.NET.Validation;

public interface ILinter
{
    LintResult Lint(RulePack resolvedPack, string sourceDirectory);
}
