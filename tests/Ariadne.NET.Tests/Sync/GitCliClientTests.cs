using System.Diagnostics;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Sync;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Resilience;

namespace Ariadne.NET.Tests.Sync;

/// <summary>Runs the real git CLI against local repositories, through the container's proxy around <see cref="GitCliClient"/>.</summary>
public sealed class GitCliClientTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ariadne-git-{Guid.NewGuid():N}");
    private readonly ServiceProvider _provider;

    public GitCliClientTests()
    {
        Directory.CreateDirectory(_root);
        var services = new ServiceCollection();

        // Production attempts with a 1 ms backoff, so the failing case stays fast.
        services.AddSingleton(new GitClientResiliencePolicies { Retry = new RetryPolicy(3, 1, false, 0) });
        services.AddAriadneNETServices();
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            // git writes its objects read-only, which Directory.Delete refuses on Windows.
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task RepositoryPathWithSpacesAndQuotes_IsOneArgument_AndSyncs()
    {
        var name = OperatingSystem.IsWindows() ? "rules repo --template x" : "rules \"repo\" --template x";
        var repo = CreateTaggedRepository(name);

        var outcome = await SyncAsync(repo);

        outcome.Result.IsSuccess.Should().BeTrue(outcome.Result.IsFailure ? outcome.Result.Error.Detail : null);
        outcome.Result.Value.ResolvedVersion.Should().Be("v1.0.0");
        File.Exists(Path.Combine(_root, "cache", "pack", "pack.yaml")).Should().BeTrue();
    }

    /// <summary>
    /// Proves the whole url, option text included, went to git as one argument right after "--": the argv echo in
    /// the error quotes each argument that holds spaces, so a split url would show up as separate words. The check
    /// does not depend on git's own, possibly translated, messages. The absent hook marker shows the template
    /// option never took effect.
    /// </summary>
    [Fact]
    public async Task UrlCarryingATemplateOption_ReachesGitAsOneRepositoryArgument_AndRunsNoHook()
    {
        var repo = CreateTaggedRepository("src");
        var marker = Path.Combine(_root, "hook-ran");
        var template = Path.Combine(_root, "evil-template");
        Directory.CreateDirectory(Path.Combine(template, "hooks"));
        var hook = Path.Combine(template, "hooks", "post-checkout");
        File.WriteAllText(hook, $"#!/bin/sh\ntouch '{marker}'\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var url = $"{repo} --template {template}";
        var outcome = await SyncAsync(url);

        outcome.Result.Error.Code.Should().Be(AriadneErrorCode.Unavailable);
        outcome.Result.Error.Detail.Should().StartWith($"git ls-remote --tags -- \"{url}\" failed with exit code ");
        File.Exists(marker).Should().BeFalse("the template option must never reach git as an option");
    }

    private async Task<ImportSyncOutcome> SyncAsync(string url)
    {
        var manifest = new ImportsManifest
        {
            Imports = [new ImportDefinition { Name = "pack", Type = "git", Url = url, Version = "^1.0" }],
        };

        var outcomes = await _provider.GetRequiredService<ISyncOrchestrator>()
            .SyncAllAsync(manifest, Path.Combine(_root, "cache"), noCache: true).ConfigureAwait(false);
        return outcomes.Single();
    }

    private string CreateTaggedRepository(string name)
    {
        var repo = Path.Combine(_root, name);
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "pack.yaml"), "name: pack\n");
        Git(repo, "init", "-q");
        Git(repo, "add", "pack.yaml");
        Git(repo, "-c", "user.name=test", "-c", "user.email=test@example.com", "commit", "-q", "-m", "init");
        Git(repo, "tag", "v1.0.0");
        return repo;
    }

    private static void Git(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var stderr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, stderr);
    }
}
