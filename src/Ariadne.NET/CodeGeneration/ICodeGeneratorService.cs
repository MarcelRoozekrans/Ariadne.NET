using Ariadne.NET.RuleSchema;

namespace Ariadne.NET.CodeGeneration;

public interface ICodeGeneratorService
{
    CodeGenerationResult Generate(RulePack resolvedPack, CodeGenerationRequest request);
}
