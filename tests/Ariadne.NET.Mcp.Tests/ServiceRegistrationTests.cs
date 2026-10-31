using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Mcp.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddAriadneNETMcpServices_ShouldResolveRuleResolutionHelper()
    {
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        services.AddAriadneNETMcpServices();
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true });

        provider.GetService<IRuleResolutionHelper>().Should().NotBeNull();
    }
}
