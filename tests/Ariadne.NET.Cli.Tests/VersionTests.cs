using System.Reflection;
using System.Text.RegularExpressions;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Cli;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Tests;

public sealed class VersionTests
{
    [Theory]
    [InlineData("--version")]
    [InlineData("--version", "not-a-command")]
    public async Task Version_prints_the_informational_version_without_build_metadata(params string[] args)
    {
        var informational = typeof(CliRootCommand).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var expected = Regex.Match(informational, "^[^+]*", RegexOptions.None, TimeSpan.FromSeconds(5)).Value;

        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        using var provider = services.BuildServiceProvider();

        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        int exitCode;
        try
        {
            exitCode = await CliRootCommand.InvokeAsync(provider, args).ConfigureAwait(true);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        exitCode.Should().Be(0);
        writer.ToString().Should().NotContain("+").And.Be(expected + Environment.NewLine);
    }
}
