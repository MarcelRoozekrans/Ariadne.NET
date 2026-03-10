using ArchGov.Core.CodeGeneration;
using ArchGov.Core.Orchestration;
using ArchGov.Core.RuleEngine;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.Validation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ArchGov.Core.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddArchGovCore_ShouldRegisterAllServices()
    {
        var services = new ServiceCollection();
        services.AddArchGovCore();
        using var provider = services.BuildServiceProvider();

        provider.GetService<IRulePackLoader>().Should().NotBeNull();
        provider.GetService<IBuiltinPackRegistry>().Should().NotBeNull();
        provider.GetService<IRulePackMerger>().Should().NotBeNull();
        provider.GetService<IRulePackResolver>().Should().NotBeNull();
        provider.GetService<IDependencyValidator>().Should().NotBeNull();
        provider.GetService<ILayerInferenceEngine>().Should().NotBeNull();
        provider.GetService<ICodeGeneratorService>().Should().NotBeNull();
        provider.GetService<IGenerateOrchestrator>().Should().NotBeNull();
        provider.GetService<ISourceValidator>().Should().NotBeNull();
        provider.GetService<ILinter>().Should().NotBeNull();
        provider.GetService<IWorkspaceOrchestrator>().Should().NotBeNull();
    }
}
