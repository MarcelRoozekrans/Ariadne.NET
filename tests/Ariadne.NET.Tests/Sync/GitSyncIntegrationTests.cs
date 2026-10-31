using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Sync;

namespace Ariadne.NET.Tests.Sync;

public sealed class GitSyncIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public GitSyncIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void FullFlow_LoadImports_SyncMocked_ResolveFromCache()
    {
        // 1. Create .architecture/imports.yaml with one import
        var archDir = Path.Combine(_tempDir, ".architecture");
        Directory.CreateDirectory(archDir);

        var importsYaml = """
            imports:
              - name: shared-rules
                type: git
                url: https://github.com/org/shared-rules.git
                version: "^1.0"
            """;
        File.WriteAllText(Path.Combine(archDir, "imports.yaml"), importsYaml);

        // 2. Load with ImportsYamlLoader and verify 1 import
        var loader = new ImportsYamlLoader();
        var manifest = loader.LoadFromFile(Path.Combine(archDir, "imports.yaml")).Value;
        manifest.Imports.Should().HaveCount(1);
        manifest.Imports[0].Name.Should().Be("shared-rules");

        // 3. Simulate cache: create .architecture/.cache/git/shared-rules/ with pack yaml and .git-info.json
        WriteSharedRulesCache(archDir);

        // 4. Create .architecture/rules.yaml with extends: [import://shared-rules]
        var rulesYaml = """
            name: my-project
            extends:
              - import://shared-rules
            """;
        File.WriteAllText(Path.Combine(archDir, "rules.yaml"), rulesYaml);

        // 5. Load user pack, resolve with RulePackResolver
        var yamlLoader = new YamlRulePackLoader();
        var userPack = yamlLoader.LoadFromFile(Path.Combine(archDir, "rules.yaml")).Value;

        var registry = new BuiltinPackRegistry(yamlLoader);
        var merger = new RulePackMerger();
        var resolver = new RulePackResolver(registry, yamlLoader, merger);

        var resolvedPack = resolver.Resolve(userPack, _tempDir).Value;

        // 6. Assert resolved pack has Domain + Application layers and Entity role
        resolvedPack.Layers.Should().HaveCount(2);
        resolvedPack.Layers.Select(l => l.Name).Should().BeEquivalentTo(["Domain", "Application"]);
        resolvedPack.ComponentRoles.Should().ContainSingle(r => r.Name == "Entity");
    }

    private static void WriteSharedRulesCache(string archDir)
    {
        var cacheDir = Path.Combine(archDir, ".cache", "git", "shared-rules");
        Directory.CreateDirectory(cacheDir);

        var sharedPackYaml = """
            name: shared-rules
            version: "1.0.0"
            layers:
              - name: Domain
                description: Core domain layer
                allowedDependencies: []
              - name: Application
                description: Application layer
                allowedDependencies:
                  - Domain
            componentRoles:
              - name: Entity
                layer: Domain
                typeKind: Class
                description: Domain entity
            namingConventions:
              namespace: "{SolutionName}.{Layer}.{Feature}"
              filePath: "src/{SolutionName}.{Layer}/{Feature}/{Name}.cs"
            """;
        File.WriteAllText(Path.Combine(cacheDir, "shared-rules.yaml"), sharedPackYaml);

        var gitInfo = new GitInfo
        {
            Url = "https://github.com/org/shared-rules.git",
            ResolvedTag = "v1.0.0",
            CommitSha = "abc123def456",
            SyncedAt = DateTimeOffset.UtcNow,
        };
        var gitInfoJson = JsonSerializer.Serialize(gitInfo, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(cacheDir, ".git-info.json"), gitInfoJson);
    }

    [Fact]
    public async Task SyncOrchestrator_WithMockedGit_ShouldReportResults()
    {
        // 1. Mock IGitSyncProvider to return success with version "v2.1.0"
        var gitProvider = Substitute.For<IGitSyncProvider>();
        gitProvider.SyncAsync(
                Arg.Any<ImportDefinition>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => new SyncResult
            {
                ImportName = callInfo.ArgAt<ImportDefinition>(0).Name,
                Success = true,
                ResolvedVersion = "v2.1.0",
            });

        // 2. Create manifest with 2 imports
        var manifest = new ImportsManifest
        {
            Imports =
            [
                new ImportDefinition { Name = "pack-a", Type = "git", Url = "https://github.com/org/pack-a.git", Version = "^2.0" },
                new ImportDefinition { Name = "pack-b", Type = "git", Url = "https://github.com/org/pack-b.git", Version = "^2.1" },
            ]
        };

        // 3. Call SyncAllAsync
        var orchestrator = new SyncOrchestrator(gitProvider);
        var results = await orchestrator.SyncAllAsync(manifest, Path.Combine(_tempDir, "cache"), noCache: false);

        // 4. Assert 2 results, all success, all with version "v2.1.0"
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.ResolvedVersion.Should().Be("v2.1.0");
        });
    }

    [Fact]
    public void CacheValidation_ShouldRespectVersionConstraints()
    {
        // 1. Create temp dir with .git-info.json for v1.2.0
        var cacheDir = Path.Combine(_tempDir, "test-cache");
        Directory.CreateDirectory(cacheDir);

        var gitInfo = new GitInfo
        {
            Url = "https://github.com/test.git",
            ResolvedTag = "v1.2.0",
            CommitSha = "deadbeef",
            SyncedAt = DateTimeOffset.UtcNow,
        };
        var json = JsonSerializer.Serialize(gitInfo, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(cacheDir, ".git-info.json"), json);

        const string url = "https://github.com/test.git";

        // 2. ^1.0 should match v1.2.0 (same major, >= 1.0.0)
        GitCliSyncProvider.IsCacheValid(cacheDir, url, SemVerRange.Parse("^1.0").Value).Should().BeTrue();

        // 3. ^2.0 should NOT match v1.2.0 (different major)
        GitCliSyncProvider.IsCacheValid(cacheDir, url, SemVerRange.Parse("^2.0").Value).Should().BeFalse();

        // 4. ~1.2 should match v1.2.0 (same major.minor, patch >= 0)
        GitCliSyncProvider.IsCacheValid(cacheDir, url, SemVerRange.Parse("~1.2").Value).Should().BeTrue();

        // 5. ~1.3 should NOT match v1.2.0 (different minor)
        GitCliSyncProvider.IsCacheValid(cacheDir, url, SemVerRange.Parse("~1.3").Value).Should().BeFalse();
    }
}
