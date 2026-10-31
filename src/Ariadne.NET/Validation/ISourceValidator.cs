using Ariadne.NET.Abstractions;

namespace Ariadne.NET.Validation;

public interface ISourceValidator
{
    SourceValidationResult Validate(RulePack resolvedPack, string sourceDirectory);
}
