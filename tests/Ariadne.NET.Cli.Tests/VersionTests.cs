using System.Reflection;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Cli;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Tests;

public sealed class VersionTests
{
    [Fact]
    public async Task Version_prints_the_informational_version_without_build_metadata()
    {
        var informational = typeof(CliRootCommand).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var expected = informational.Split('+')[0];

        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        using var provider = services.BuildServiceProvider();

        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        int exitCode;
        try
        {
            exitCode = await CliRootCommand.InvokeAsync(provider, ["--version"]).ConfigureAwait(true);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        exitCode.Should().Be(0);
        writer.ToString().Should().Be(expected + Environment.NewLine);
    }
}
