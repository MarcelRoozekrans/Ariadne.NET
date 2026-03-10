using ArchGov.Core.CodeGeneration;
using ArchGov.Core.Orchestration;
using ArchGov.Core.RuleEngine;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace ArchGov.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddArchGovCore(this IServiceCollection services)
    {
        services.AddSingleton<IRulePackLoader, YamlRulePackLoader>();
        services.AddSingleton<IBuiltinPackRegistry, BuiltinPackRegistry>();
        services.AddSingleton<IRulePackMerger, RulePackMerger>();
        services.AddSingleton<IRulePackResolver, RulePackResolver>();
        services.AddSingleton<IDependencyValidator, DependencyValidator>();
        services.AddSingleton<ILayerInferenceEngine, LayerInferenceEngine>();
        services.AddSingleton<ICodeGeneratorService, CodeGeneratorService>();
        services.AddSingleton<IGenerateOrchestrator, GenerateOrchestrator>();
        services.AddSingleton<ISourceValidator, SourceValidator>();

        return services;
    }
}
