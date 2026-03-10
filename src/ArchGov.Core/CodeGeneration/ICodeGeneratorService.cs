using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.CodeGeneration;

public interface ICodeGeneratorService
{
    CodeGenerationResult Generate(RulePack resolvedPack, CodeGenerationRequest request);
}
