using FluentAssertions;
using NSubstitute;
using Ariadne.NET.Sync;

namespace Ariadne.NET.Tests.Sync;

public class SyncOrchestratorTests
{
    [Fact]
    public async Task SyncAllAsync_ShouldSyncEachImport()
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        gitProvider.SyncAsync(Arg.Any<ImportDefinition>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SyncResult
            {
                ImportName = callInfo.ArgAt<ImportDefinition>(0).Name,
                Success = true,
                ResolvedVersion = "v1.0.0"
            });

        var sut = new SyncOrchestrator(gitProvider);
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "pack-a", Type = "git", Url = "https://example.com/a.git", Version = "^1.0" },
                new ImportDefinition { Name = "pack-b", Type = "git", Url = "https://example.com/b.git", Version = "^2.0" }
            ]
        };

        var results = await sut.SyncAllAsync(manifest, "/tmp/cache", false);

        results.Should().HaveCount(2);
        results.Select(r => r.ImportName).Should().BeEquivalentTo(["pack-a", "pack-b"]);
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }

    [Fact]
    public async Task SyncByNameAsync_ShouldSyncOnlyMatchingImport()
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        gitProvider.SyncAsync(Arg.Any<ImportDefinition>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SyncResult
            {
                ImportName = callInfo.ArgAt<ImportDefinition>(0).Name,
                Success = true,
                ResolvedVersion = "v2.0.0"
            });

        var sut = new SyncOrchestrator(gitProvider);
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "pack-a", Type = "git", Url = "https://example.com/a.git", Version = "^1.0" },
                new ImportDefinition { Name = "pack-b", Type = "git", Url = "https://example.com/b.git", Version = "^2.0" }
            ]
        };

        var results = await sut.SyncByNameAsync(manifest, "pack-b", "/tmp/cache", false);

        results.Should().ContainSingle();
        results[0].ImportName.Should().Be("pack-b");
        results[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task SyncByNameAsync_NameNotFound_ShouldReturnError()
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        var sut = new SyncOrchestrator(gitProvider);
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "pack-a", Type = "git", Url = "https://example.com/a.git", Version = "^1.0" }
            ]
        };

        var results = await sut.SyncByNameAsync(manifest, "nonexistent", "/tmp/cache", false);

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain("nonexistent");
    }

    [Fact]
    public async Task SyncAllAsync_UnsupportedType_ShouldReturnError()
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        var sut = new SyncOrchestrator(gitProvider);
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "nuget-pack", Type = "nuget", Url = "https://example.com/pkg", Version = "1.0.0" }
            ]
        };

        var results = await sut.SyncAllAsync(manifest, "/tmp/cache", false);

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain("nuget");
    }

    [Fact]
    public async Task SyncAllAsync_PartialFailure_ShouldReportBothSuccessAndFailure()
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        gitProvider.SyncAsync(
                Arg.Is<ImportDefinition>(i => i.Name == "good-pack"),
                Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new SyncResult { ImportName = "good-pack", ResolvedVersion = "v1.0.0" });
        gitProvider.SyncAsync(
                Arg.Is<ImportDefinition>(i => i.Name == "bad-pack"),
                Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new SyncResult { ImportName = "bad-pack", Success = false, Error = "No matching tag" });

        var sut = new SyncOrchestrator(gitProvider);
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "good-pack", Type = "git", Url = "https://example.com/good.git", Version = "^1.0" },
                new ImportDefinition { Name = "bad-pack", Type = "git", Url = "https://example.com/bad.git", Version = "^9.0" },
            ],
        };

        var results = await sut.SyncAllAsync(manifest, "/tmp/cache", false);

        results.Should().HaveCount(2);
        results.Should().ContainSingle(r => r.Success && r.ImportName == "good-pack");
        results.Should().ContainSingle(r => !r.Success && r.ImportName == "bad-pack");
    }

    [Fact]
    public async Task SyncAllAsync_MixedTypes_ShouldHandleGitAndRejectOthers()
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        gitProvider.SyncAsync(Arg.Any<ImportDefinition>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SyncResult
            {
                ImportName = callInfo.ArgAt<ImportDefinition>(0).Name,
                ResolvedVersion = "v1.0.0",
            });

        var sut = new SyncOrchestrator(gitProvider);
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "git-pack", Type = "git", Url = "https://example.com/a.git", Version = "^1.0" },
                new ImportDefinition { Name = "nuget-pack", Type = "nuget", Url = "https://nuget.org/pkg", Version = "1.0.0" },
            ],
        };

        var results = await sut.SyncAllAsync(manifest, "/tmp/cache", false);

        results.Should().HaveCount(2);
        results[0].Success.Should().BeTrue();
        results[1].Success.Should().BeFalse();
        results[1].Error.Should().Contain("nuget");
    }
}
