using Ariadne.NET.Abstractions;

namespace Ariadne.NET.CodeGeneration;

public interface ICodeGeneratorService
{
    CodeGenerationResult Generate(RulePack resolvedPack, CodeGenerationRequest request);
}
