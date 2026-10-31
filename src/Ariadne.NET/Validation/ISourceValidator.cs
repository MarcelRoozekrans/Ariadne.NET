using Ariadne.NET.RuleSchema;

namespace Ariadne.NET.Validation;

public interface ISourceValidator
{
    SourceValidationResult Validate(RulePack resolvedPack, string sourceDirectory);
}
