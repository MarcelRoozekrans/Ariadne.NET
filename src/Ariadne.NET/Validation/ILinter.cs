using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Validation;

public interface ILinter
{
    LintResult Lint(RulePack resolvedPack, string sourceDirectory);
}
