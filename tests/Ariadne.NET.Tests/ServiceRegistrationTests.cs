using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.CodeGeneration;
using Ariadne.NET.Orchestration;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.Validation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddAriadneNETServices_ShouldRegisterAllServices()
    {
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        using var provider = services.BuildServiceProvider();

        provider.GetService<IRulePackLoader>().Should().NotBeNull();
        provider.GetService<IBuiltinPackRegistry>().Should().NotBeNull();
        provider.GetService<IRulePackMerger>().Should().NotBeNull();
        provider.GetService<IRulePackResolver>().Should().NotBeNull();
        provider.GetService<ILayerInferenceEngine>().Should().NotBeNull();
        provider.GetService<ICodeGeneratorService>().Should().NotBeNull();
        provider.GetService<IGenerateOrchestrator>().Should().NotBeNull();
        provider.GetService<ILinter>().Should().NotBeNull();
        provider.GetService<IWorkspaceOrchestrator>().Should().NotBeNull();
    }

    [Fact]
    public void ServiceRegistration_ResolvesIArchitectureValidator()
    {
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        using var provider = services.BuildServiceProvider();

        provider.GetService<IArchitectureValidator>().Should().BeOfType<ArchitectureValidator>();
    }
}
