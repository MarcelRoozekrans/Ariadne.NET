using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.Validation;

public interface ISourceValidator
{
    SourceValidationResult Validate(RulePack resolvedPack, string sourceDirectory);
}
