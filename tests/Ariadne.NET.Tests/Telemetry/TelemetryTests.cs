using System.Diagnostics;
using System.Diagnostics.Metrics;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Sync;
using Ariadne.NET.Telemetry;
using Ariadne.NET.Tests.Sync;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Resilience;
using ZeroAlloc.Results;

namespace Ariadne.NET.Tests.Telemetry;

/// <summary>
/// Drives the pack and sync operations through the services <c>AddAriadneNETServices</c> builds, and reads their spans
/// and the duration histogram with BCL listeners.
/// </summary>
public sealed class TelemetryTests : IDisposable
{
    private const string ResolveSpan = "ariadne.resolve";
    private const string GetPackSpan = "ariadne.get_pack";
    private const string LoadSpan = "ariadne.load";
    private const string SyncImportSpan = "ariadne.sync_import";

    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), $"ariadne-telemetry-{Guid.NewGuid():N}");
    private readonly FakeGitScript _git = new();
    private readonly ServiceProvider _provider;

    private static readonly ImportDefinition Import =
        new() { Name = "pack", Type = "git", Url = "https://user:secret@example.com/pack.git", Version = "^1.0" };

    public TelemetryTests()
    {
        var services = new ServiceCollection();

        // A 1 ms backoff keeps the retry test fast; registered first, so the generated TryAddSingleton keeps it.
        services.AddSingleton(new GitClientResiliencePolicies { Retry = new RetryPolicy(3, 1, false, 0) });
        services.AddAriadneNETServices();

        // The container builds the resilience proxy around the fake instead of GitCliClient.
        services.AddSingleton(_git);
        services.AddGitClientResilience<FakeGitClient>();
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        if (Directory.Exists(_cacheDir))
        {
            Directory.Delete(_cacheDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveUnknownBuiltin_IsOneErrorSpan_TaggedNotFound()
    {
        using var capture = new TelemetryCapture();

        var result = Resolve("builtin://no-such-pack");

        result.IsFailure.Should().BeTrue();
        var span = capture.Spans(ResolveSpan).Should().ContainSingle().Subject;
        span.Status.Should().Be(ActivityStatusCode.Error);
        TagText(span, AriadneTelemetry.ErrorCode).Should().Be(nameof(AriadneErrorCode.NotFound));
        span.GetTagItem(AriadneTelemetry.PackName).Should().Be("user");

        // The registry's own span is a child of the resolve span and fails the same way.
        var getPack = capture.Spans(GetPackSpan).Should().ContainSingle().Subject;
        getPack.ParentSpanId.Should().Be(span.SpanId);
        getPack.Status.Should().Be(ActivityStatusCode.Error);
        getPack.GetTagItem(AriadneTelemetry.PackName).Should().Be("no-such-pack");

        var point = capture.Points(AriadneTelemetry.OperationDuration)
            .Should().ContainSingle(p => Equals(p.Tag(AriadneTelemetry.Operation), "resolve")).Subject;
        point.Tag(AriadneTelemetry.ErrorCode)?.ToString().Should().Be(nameof(AriadneErrorCode.NotFound));
    }

    [Fact]
    public void ResolveKnownBuiltin_IsNotAnErrorSpan_AndHasNoErrorTag()
    {
        using var capture = new TelemetryCapture();

        var result = Resolve("builtin://clean-architecture");

        result.IsSuccess.Should().BeTrue();
        var span = capture.Spans(ResolveSpan).Should().ContainSingle().Subject;
        span.Status.Should().BeOneOf(ActivityStatusCode.Unset, ActivityStatusCode.Ok);
        span.GetTagItem(AriadneTelemetry.ErrorCode).Should().BeNull();

        capture.Spans(GetPackSpan).Should().ContainSingle()
            .Which.GetTagItem(AriadneTelemetry.PackName).Should().Be("clean-architecture");

        var point = capture.Points(AriadneTelemetry.OperationDuration)
            .Should().ContainSingle(p => Equals(p.Tag(AriadneTelemetry.Operation), "resolve")).Subject;
        point.HasTag(AriadneTelemetry.ErrorCode).Should().BeFalse();
        point.Value.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void LoadFromString_TagsNothingIdentifying()
    {
        using var capture = new TelemetryCapture();

        var result = _provider.GetRequiredService<IRulePackLoader>().LoadFromString("name: [unclosed");

        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        var span = capture.Spans(LoadSpan).Should().ContainSingle().Subject;
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.TagObjects.Select(tag => tag.Key).Should().Equal(AriadneTelemetry.ErrorCode);
        TagText(span, AriadneTelemetry.ErrorCode).Should().Be(nameof(AriadneErrorCode.Invalid));
    }

    [Fact]
    public async Task SyncImport_ThatRetries_IsOneSpan_TaggedWithTheImportNameOnly()
    {
        _git.LsRemote = (attempt, _) => attempt < 3 ? throw new InvalidOperationException("flaky") : FakeGitScript.Tags;
        using var capture = new TelemetryCapture();

        var result = await SyncAsync();

        result.IsSuccess.Should().BeTrue();
        _git.LsRemoteCalls.Should().Be(3);
        var span = capture.Spans(SyncImportSpan).Should().ContainSingle().Subject;
        span.Status.Should().BeOneOf(ActivityStatusCode.Unset, ActivityStatusCode.Ok);
        span.TagObjects.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, object?>(AriadneTelemetry.ImportName, "pack"));

        capture.Points(AriadneTelemetry.OperationDuration)
            .Should().ContainSingle(p => Equals(p.Tag(AriadneTelemetry.Operation), "sync_import"))
            .Which.HasTag(AriadneTelemetry.ErrorCode).Should().BeFalse();
    }

    [Fact]
    public async Task SyncImport_WhenGitKeepsFailing_IsOneErrorSpan_TaggedUnavailable()
    {
        _git.LsRemote = (_, _) => throw new InvalidOperationException("fatal: repository not found");
        using var capture = new TelemetryCapture();

        var result = await SyncAsync();

        result.Error.Code.Should().Be(AriadneErrorCode.Unavailable);
        var span = capture.Spans(SyncImportSpan).Should().ContainSingle().Subject;
        span.Status.Should().Be(ActivityStatusCode.Error);
        TagText(span, AriadneTelemetry.ErrorCode).Should().Be(nameof(AriadneErrorCode.Unavailable));
        capture.Points(AriadneTelemetry.OperationDuration).Should().ContainSingle()
            .Which.Tag(AriadneTelemetry.ErrorCode)?.ToString().Should().Be(nameof(AriadneErrorCode.Unavailable));
    }

    [Fact]
    public async Task SyncImport_CancelledByTheCaller_IsNotAnErrorSpan()
    {
        _git.LsRemote = (_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return FakeGitScript.Tags;
        };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var capture = new TelemetryCapture();

        var act = () => SyncAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        var span = capture.Spans(SyncImportSpan).Should().ContainSingle().Subject;
        span.Status.Should().NotBe(ActivityStatusCode.Error);
        span.GetTagItem("error.type").Should().BeNull();

        var point = capture.Points(AriadneTelemetry.OperationDuration).Should().ContainSingle().Subject;
        point.HasTag(AriadneTelemetry.ErrorCode).Should().BeFalse();
        point.HasTag("error.type").Should().BeFalse();
    }

    [Fact]
    public async Task WithoutAListener_TheResultsAreTheSame()
    {
        using (var probe = new ActivitySource(AriadneTelemetry.SourceName))
        {
            probe.HasListeners().Should().BeFalse("the first run must take the proxies' no-listener path");
        }

        var unlistened = await RunEveryOperationAsync();

        Snapshot listened;
        using (new TelemetryCapture())
        {
            listened = await RunEveryOperationAsync();
        }

        listened.UnknownResolve.Error.Should().Be(unlistened.UnknownResolve.Error);
        listened.KnownResolve.Value.Should().BeEquivalentTo(unlistened.KnownResolve.Value);
        listened.BadLoad.Error.Should().Be(unlistened.BadLoad.Error);
        listened.Sync.Value.Should().Be(unlistened.Sync.Value);
        listened.FailedSync.Error.Should().Be(unlistened.FailedSync.Error);
    }

    [Fact]
    public async Task DurationHistograms_AdviseTheDurationBuckets()
    {
        // Runs a pack and a sync operation first, so both proxies have created their histogram.
        await RunEveryOperationAsync();
        var histograms = new List<Histogram<double>>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (instrument is Histogram<double> histogram
                    && string.Equals(instrument.Meter.Name, AriadneTelemetry.SourceName, StringComparison.Ordinal))
                {
                    histograms.Add(histogram);
                }
            },
        };

        listener.Start();

        histograms.Should().HaveCount(2, "the pack and the sync operations each have a proxy with its own meter");
        histograms.Should().AllSatisfy(histogram =>
        {
            histogram.Name.Should().Be(AriadneTelemetry.OperationDuration);
            histogram.Unit.Should().Be("s");
            histogram.Advice!.HistogramBucketBoundaries.Should().Equal(AriadneTelemetry.DurationBuckets.ToArray());
        });
    }

    private async Task<Snapshot> RunEveryOperationAsync()
    {
        _git.LsRemote = (_, _) => FakeGitScript.Tags;
        var sync = await SyncAsync().ConfigureAwait(false);
        _git.LsRemote = (_, _) => throw new InvalidOperationException("fatal: repository not found");
        var failedSync = await SyncAsync().ConfigureAwait(false);

        return new Snapshot(
            Resolve("builtin://no-such-pack"),
            Resolve("builtin://clean-architecture"),
            _provider.GetRequiredService<IRulePackLoader>().LoadFromString("name: [unclosed"),
            sync,
            failedSync);
    }

    private Result<RulePack, AriadneError> Resolve(string reference) =>
        _provider.GetRequiredService<IRulePackResolver>()
            .Resolve(new RulePack { Name = "user", Extends = [reference] });

    private Task<Result<SyncedImport, AriadneError>> SyncAsync(CancellationToken ct = default) =>
        _provider.GetRequiredService<IGitSyncProvider>().SyncAsync(Import, _cacheDir, noCache: true, ct);

    private static string? TagText(Activity span, string key) => span.GetTagItem(key)?.ToString();

    private sealed record Snapshot(
        Result<RulePack, AriadneError> UnknownResolve,
        Result<RulePack, AriadneError> KnownResolve,
        Result<RulePack, AriadneError> BadLoad,
        Result<SyncedImport, AriadneError> Sync,
        Result<SyncedImport, AriadneError> FailedSync);
}
