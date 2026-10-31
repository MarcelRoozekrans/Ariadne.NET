using FluentAssertions;
using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;

namespace Ariadne.NET.Tests.RuleLoader;

public class RulePackResolverTests
{
    [Fact]
    public void Resolve_ShouldMergeBuiltinExtensions()
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var merger = new RulePackMerger();
        var resolver = new RulePackResolver(registry, new YamlRulePackLoader(), merger);

        var userPack = new RulePack
        {
            Name = "my-project",
            Extends = ["builtin://clean-architecture", "builtin://cqrs"],
        };

        var resolved = resolver.Resolve(userPack).Value;

        resolved.Layers.Should().NotBeEmpty();
        resolved.ComponentRoles.Select(r => r.Name).Should().Contain("Command");
        resolved.ComponentRoles.Select(r => r.Name).Should().Contain("Entity");
    }

    [Fact]
    public void Resolve_ImportProtocol_ShouldLoadFromCacheDirectory()
    {
        // Create temp dir with cached pack
        var tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-resolver-{Guid.NewGuid():N}");
        var cacheDir = Path.Combine(tempDir, ".architecture", ".cache", "git", "my-pack");
        Directory.CreateDirectory(cacheDir);

        try
        {
            File.WriteAllText(Path.Combine(cacheDir, "my-pack.yaml"), """
                name: my-pack
                layers:
                  - name: Domain
                    allowedDependencies: []
                  - name: Application
                    allowedDependencies:
                      - Domain
                """);

            var loader = new YamlRulePackLoader();
            var registry = new BuiltinPackRegistry(loader);
            var merger = new RulePackMerger();
            var resolver = new RulePackResolver(registry, loader, merger);

            var userPack = new RulePack
            {
                Name = "user-rules",
                Extends = ["import://my-pack"],
            };

            var result = resolver.Resolve(userPack, tempDir).Value;

            result.Layers.Should().Contain(l => l.Name == "Domain");
            result.Layers.Should().Contain(l => l.Name == "Application");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Resolve_ImportWithoutWorkingDirectory_TellsUserToRunAriadneSync()
    {
        var loader = new YamlRulePackLoader();
        var resolver = new RulePackResolver(new BuiltinPackRegistry(loader), loader, new RulePackMerger());

        var userPack = new RulePack
        {
            Name = "user-rules",
            Extends = ["import://team-rules"],
        };

        var result = resolver.Resolve(userPack);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("Run 'ariadne sync'");
    }

    [Fact]
    public void Resolve_ImportProtocol_NoCacheDir_ReturnsNotFound()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-resolver-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var loader = new YamlRulePackLoader();
            var registry = new BuiltinPackRegistry(loader);
            var merger = new RulePackMerger();
            var resolver = new RulePackResolver(registry, loader, merger);

            var userPack = new RulePack
            {
                Name = "user-rules",
                Extends = ["import://nonexistent"],
            };

            var result = resolver.Resolve(userPack, tempDir);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
            result.Error.Message.Should().Contain("ariadne sync");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Resolve_ImportProtocol_CacheDirWithoutYaml_ReturnsNotFound()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-resolver-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempDir, ".architecture", ".cache", "git", "empty-pack"));

        try
        {
            var result = CreateResolver().Resolve(
                new RulePack { Name = "user-rules", Extends = ["import://empty-pack"] },
                tempDir);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
            result.Error.Message.Should().Contain("ariadne sync");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Resolve_UnsupportedScheme_ReturnsInvalid()
    {
        var result = CreateResolver().Resolve(new RulePack { Name = "user-rules", Extends = ["http://x"] });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("http://x");
    }

    [Fact]
    public void Resolve_UnknownBuiltin_ReturnsNotFound()
    {
        var result = CreateResolver().Resolve(new RulePack { Name = "user-rules", Extends = ["builtin://nope"] });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
        result.Error.Message.Should().Be("Built-in pack 'nope' not found.");
        result.Error.Detail.Should().Contain("clean-architecture");
    }

    [Fact]
    public void Resolve_StopsAtFirstFailingReference()
    {
        var result = CreateResolver().Resolve(new RulePack
        {
            Name = "user-rules",
            Extends = ["builtin://clean-architecture", "builtin://first-bad", "builtin://second-bad"],
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("first-bad");
        result.Error.Message.Should().NotContain("second-bad");
    }

    [Fact]
    public void Resolve_ImportedPackIsMalformed_ReturnsErrorNamingTheCacheFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-resolver-{Guid.NewGuid():N}");
        var cacheDir = Path.Combine(tempDir, ".architecture", ".cache", "git", "bad-pack");
        Directory.CreateDirectory(cacheDir);

        try
        {
            var packFile = Path.Combine(cacheDir, "bad-pack.yaml");
            File.WriteAllText(packFile, "name: x\nlayers: [unclosed");

            var result = CreateResolver().Resolve(
                new RulePack { Name = "user-rules", Extends = ["import://bad-pack"] },
                tempDir);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
            result.Error.Message.Should().StartWith($"Rule pack file '{packFile}' could not be parsed: ");
            result.Error.Detail.Should().Be("line 3, column 1");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static RulePackResolver CreateResolver()
    {
        var loader = new YamlRulePackLoader();
        return new RulePackResolver(new BuiltinPackRegistry(loader), loader, new RulePackMerger());
    }
}
