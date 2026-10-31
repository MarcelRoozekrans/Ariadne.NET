using Ariadne.NET.Abstractions;
using Ariadne.NET.CodeGeneration;
using Ariadne.NET.RuleEngine;

using ZInject;

namespace Ariadne.NET.Orchestration;

[Singleton]
public sealed class GenerateOrchestrator : IGenerateOrchestrator
{
    private readonly ILayerInferenceEngine _inferenceEngine;
    private readonly ICodeGeneratorService _codeGenerator;

    public GenerateOrchestrator(
        ILayerInferenceEngine inferenceEngine,
        ICodeGeneratorService codeGenerator)
    {
        _inferenceEngine = inferenceEngine;
        _codeGenerator = codeGenerator;
    }

    public GenerateResult Generate(RulePack resolvedPack, GenerateRequest request)
    {
        ArgumentNullException.ThrowIfNull(resolvedPack);
        ArgumentNullException.ThrowIfNull(request);

        var generatedFiles = new List<CodeGenerationResult>();
        var errors = new List<string>();

        foreach (var component in request.Components)
        {
            var inference = _inferenceEngine.Infer(resolvedPack, component.RoleName);

            if (inference.Status != LayerInferenceStatus.Ok)
            {
                var message = inference.Status == LayerInferenceStatus.Ambiguous
                    ? $"Ambiguous role '{component.RoleName}': {inference.Message}"
                    : $"Invalid role '{component.RoleName}': {inference.Message}";
                errors.Add(message);
                continue;
            }

            var codeRequest = new CodeGenerationRequest
            {
                SolutionName = request.SolutionName,
                Feature = request.Feature,
                ComponentName = component.Name,
                RoleName = component.RoleName,
            };

            var result = _codeGenerator.Generate(resolvedPack, codeRequest);
            generatedFiles.Add(result);
        }

        return new GenerateResult
        {
            Success = errors.Count == 0,
            GeneratedFiles = generatedFiles,
            Errors = errors,
        };
    }
}
