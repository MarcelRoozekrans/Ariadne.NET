using System.ComponentModel;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Sync;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Resilience;

namespace Ariadne.NET.Tests.Sync;

/// <summary>
/// Drives sync through the container: the real <see cref="ISyncOrchestrator"/> and
/// <see cref="GitCliSyncProvider"/>, the generated resilience proxy, and a fake inner git client.
/// </summary>
public sealed class GitClientResilienceTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), $"ariadne-resilience-{Guid.NewGuid():N}");
    private readonly FakeGitScript _git = new();
    private readonly ServiceProvider _provider;

    private static readonly ImportsManifest Manifest = new()
    {
        Imports = [new ImportDefinition { Name = "pack", Type = "git", Url = "https://example.com/pack.git", Version = "^1.0" }],
    };

    public GitClientResilienceTests()
    {
        // Same attempts as production, but a 1 ms backoff so the retry tests stay fast.
        // Production policies are asserted in DefaultPolicies_AreTheProductionValues.
        _provider = BuildProvider(new GitClientResiliencePolicies { Retry = new RetryPolicy(3, 1, false, 0) });
    }

    private ServiceProvider BuildProvider(GitClientResiliencePolicies policies)
    {
        var services = new ServiceCollection();

        // Registered first, so the generated TryAddSingleton keeps it.
        services.AddSingleton(policies);
        services.AddAriadneNETServices();

        // Replaces GitCliClient: the container now builds the proxy around the fake.
        services.AddSingleton(_git);
        services.AddGitClientResilience<FakeGitClient>();
        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        if (Directory.Exists(_cacheDir))
        {
            Directory.Delete(_cacheDir, recursive: true);
        }
    }

    private Task<IReadOnlyList<ImportSyncOutcome>> SyncAllAsync(CancellationToken ct = default) =>
        _provider.GetRequiredService<ISyncOrchestrator>().SyncAllAsync(Manifest, _cacheDir, noCache: true, ct);

    [Fact]
    public void Container_HandsTheProviderTheResilienceProxy()
    {
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IGitClient>().Should().BeOfType<IGitClientResilienceProxy>();
    }

    [Fact]
    public void DefaultPolicies_AreTheProductionValues()
    {
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        using var provider = services.BuildServiceProvider();

        var policies = provider.GetRequiredService<GitClientResiliencePolicies>();
        policies.Retry.MaxAttempts.Should().Be(3);
        policies.Retry.BackoffMs.Should().Be(500);
        policies.Timeout.TotalMs.Should().Be(60_000);
    }

    [Fact]
    public async Task ThrowsTwiceThenReturns_Succeeds_AfterThreeCalls()
    {
        _git.LsRemote = (attempt, _) => attempt < 3 ? throw new InvalidOperationException("flaky") : FakeGitScript.Tags;

        var outcomes = await SyncAllAsync();

        outcomes.Should().ContainSingle();
        outcomes[0].Result.IsSuccess.Should().BeTrue();
        outcomes[0].Result.Value.ResolvedVersion.Should().Be("v1.0.0");
        _git.LsRemoteCalls.Should().Be(3);
    }

    [Fact]
    public async Task AlwaysThrows_IsUnavailable_WithoutThrowing_AfterThreeCalls()
    {
        _git.LsRemote = (_, _) => throw new InvalidOperationException("git ls-remote failed with exit code 128: fatal: repository not found");

        var outcomes = await SyncAllAsync();

        outcomes.Should().ContainSingle();
        var error = outcomes[0].Result.Error;
        error.Code.Should().Be(AriadneErrorCode.Unavailable);
        error.Message.Should().Contain("pack");
        error.Detail.Should().Contain("fatal: repository not found");
        _git.LsRemoteCalls.Should().Be(3);
        _git.CloneCalls.Should().Be(0);
    }

    [Fact]
    public async Task GitMissing_Win32Exception_IsUnavailable()
    {
        _git.LsRemote = (_, _) => throw new Win32Exception(2, "No such file or directory");

        var outcomes = await SyncAllAsync();

        outcomes[0].Result.Error.Code.Should().Be(AriadneErrorCode.Unavailable);
        outcomes[0].Result.Error.Detail.Should().Be("No such file or directory");
    }

    [Fact]
    public async Task PreCancelledToken_PropagatesOperationCanceled_AndIsNotRetried()
    {
        _git.LsRemote = (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return FakeGitScript.Tags;
        };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => SyncAllAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _git.LsRemoteCalls.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task CancelledDuringAnAttemptThatThrows_PropagatesOperationCanceled_AndIsNotRetried()
    {
        using var cts = new CancellationTokenSource();
        _git.LsRemote = (_, _) =>
        {
            // The caller cancels while git runs, and git then fails with an ordinary error.
            cts.Cancel();
            throw new InvalidOperationException("git was interrupted");
        };

        var act = () => SyncAllAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _git.LsRemoteCalls.Should().Be(1);
    }

    [Fact]
    public async Task GitOutlivesTheTimeout_IsUnavailable_AndSaysSo()
    {
        _git.LsRemote = (_, ct) =>
        {
            ct.WaitHandle.WaitOne();
            ct.ThrowIfCancellationRequested();
            return FakeGitScript.Tags;
        };
        using var provider = BuildProvider(new GitClientResiliencePolicies
        {
            Retry = new RetryPolicy(3, 1, false, 0),
            Timeout = new TimeoutPolicy(50),
        });

        var outcomes = await provider.GetRequiredService<ISyncOrchestrator>()
            .SyncAllAsync(Manifest, _cacheDir, noCache: true);

        outcomes[0].Result.Error.Code.Should().Be(AriadneErrorCode.Unavailable);
        outcomes[0].Result.Error.Detail.Should().Be("git did not finish before the timeout.");
        _git.LsRemoteCalls.Should().Be(1, "the timeout covers the whole call, retries included");
    }
}
