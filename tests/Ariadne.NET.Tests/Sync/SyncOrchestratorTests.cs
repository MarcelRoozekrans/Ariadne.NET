using Ariadne.NET.Abstractions;
using Ariadne.NET.Sync;
using FluentAssertions;
using NSubstitute;
using ZeroAlloc.Results;

namespace Ariadne.NET.Tests.Sync;

public class SyncOrchestratorTests
{
    private static Result<SyncedImport, AriadneError> Synced(ImportDefinition import, string version) =>
        Result<SyncedImport, AriadneError>.Success(new SyncedImport(import.Name, version, WasCached: false, $"/tmp/cache/{import.Name}"));

    private static IGitSyncProvider ProviderReturning(string version)
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        gitProvider.SyncAsync(Arg.Any<ImportDefinition>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Synced(callInfo.ArgAt<ImportDefinition>(0), version));
        return gitProvider;
    }

    [Fact]
    public async Task SyncAllAsync_ShouldSyncEachImport()
    {
        var sut = new SyncOrchestrator(ProviderReturning("v1.0.0"));
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "pack-a", Type = "git", Url = "https://example.com/a.git", Version = "^1.0" },
                new ImportDefinition { Name = "pack-b", Type = "git", Url = "https://example.com/b.git", Version = "^2.0" }
            ]
        };

        var outcomes = await sut.SyncAllAsync(manifest, "/tmp/cache", false);

        outcomes.Should().HaveCount(2);
        outcomes.Select(o => o.ImportName).Should().BeEquivalentTo(["pack-a", "pack-b"]);
        outcomes.Should().AllSatisfy(o => o.Result.IsSuccess.Should().BeTrue());
    }

    [Fact]
    public async Task SyncByNameAsync_ShouldSyncOnlyMatchingImport()
    {
        var sut = new SyncOrchestrator(ProviderReturning("v2.0.0"));
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "pack-a", Type = "git", Url = "https://example.com/a.git", Version = "^1.0" },
                new ImportDefinition { Name = "pack-b", Type = "git", Url = "https://example.com/b.git", Version = "^2.0" }
            ]
        };

        var outcomes = await sut.SyncByNameAsync(manifest, "pack-b", "/tmp/cache", false);

        outcomes.Should().ContainSingle();
        outcomes[0].ImportName.Should().Be("pack-b");
        outcomes[0].Result.Value.ResolvedVersion.Should().Be("v2.0.0");
    }

    [Fact]
    public async Task SyncByNameAsync_NameNotFound_ReturnsSingleNotFoundOutcome()
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

        var outcomes = await sut.SyncByNameAsync(manifest, "nonexistent", "/tmp/cache", false);

        outcomes.Should().ContainSingle();
        outcomes[0].ImportName.Should().Be("nonexistent");
        outcomes[0].Result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
        outcomes[0].Result.Error.Message.Should().Be("Import 'nonexistent' not found in manifest.");
        gitProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task SyncAllAsync_UnsupportedType_IsInvalid()
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

        var outcomes = await sut.SyncAllAsync(manifest, "/tmp/cache", false);

        outcomes.Should().ContainSingle();
        outcomes[0].Result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        outcomes[0].Result.Error.Message.Should().Be("Unsupported import type 'nuget'. Only 'git' is currently supported.");
    }

    [Fact]
    public async Task SyncAllAsync_OneImportFails_TheOtherStillSyncs()
    {
        var gitProvider = Substitute.For<IGitSyncProvider>();
        gitProvider.SyncAsync(
                Arg.Is<ImportDefinition>(i => i.Name == "bad-pack"),
                Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<SyncedImport, AriadneError>.Failure(new AriadneError(AriadneErrorCode.NotFound, "No matching tag")));
        gitProvider.SyncAsync(
                Arg.Is<ImportDefinition>(i => i.Name == "good-pack"),
                Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Synced(callInfo.ArgAt<ImportDefinition>(0), "v1.0.0"));

        var sut = new SyncOrchestrator(gitProvider);
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "bad-pack", Type = "git", Url = "https://example.com/bad.git", Version = "^9.0" },
                new ImportDefinition { Name = "good-pack", Type = "git", Url = "https://example.com/good.git", Version = "^1.0" },
            ],
        };

        var outcomes = await sut.SyncAllAsync(manifest, "/tmp/cache", false);

        outcomes.Should().HaveCount(2);
        outcomes.Should().ContainSingle(o => o.Result.IsFailure && o.ImportName == "bad-pack");
        outcomes.Should().ContainSingle(o => o.Result.IsSuccess && o.ImportName == "good-pack");
    }

    [Fact]
    public async Task SyncAllAsync_MixedTypes_ShouldHandleGitAndRejectOthers()
    {
        var sut = new SyncOrchestrator(ProviderReturning("v1.0.0"));
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "git-pack", Type = "git", Url = "https://example.com/a.git", Version = "^1.0" },
                new ImportDefinition { Name = "nuget-pack", Type = "nuget", Url = "https://nuget.org/pkg", Version = "1.0.0" },
            ],
        };

        var outcomes = await sut.SyncAllAsync(manifest, "/tmp/cache", false);

        outcomes.Should().HaveCount(2);
        outcomes[0].Result.IsSuccess.Should().BeTrue();
        outcomes[1].Result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        outcomes[1].Result.Error.Message.Should().Contain("nuget");
    }

    [Fact]
    public async Task SyncAllAsync_ImportWithNullNameAndType_DoesNotThrow()
    {
        var sut = new SyncOrchestrator(Substitute.For<IGitSyncProvider>());
        var manifest = new ImportsManifest
        {
            Imports = [new ImportDefinition { Name = null!, Type = null!, Url = null!, Version = null! }],
        };

        var outcomes = await sut.SyncAllAsync(manifest, "/tmp/cache", false);

        outcomes.Should().ContainSingle();
        outcomes[0].ImportName.Should().BeEmpty();
        outcomes[0].Result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
    }

    [Fact]
    public async Task SyncByNameAsync_ManifestWithNullName_DoesNotThrow()
    {
        var sut = new SyncOrchestrator(ProviderReturning("v1.0.0"));
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = null!, Type = "git", Url = "https://example.com/a.git", Version = "^1.0" },
                new ImportDefinition { Name = "pack-b", Type = "git", Url = "https://example.com/b.git", Version = "^1.0" },
            ],
        };

        var outcomes = await sut.SyncByNameAsync(manifest, "pack-b", "/tmp/cache", false);

        outcomes.Should().ContainSingle(o => o.ImportName == "pack-b" && o.Result.IsSuccess);
    }
}
