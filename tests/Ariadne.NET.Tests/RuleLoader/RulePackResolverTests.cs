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

        var resolved = resolver.Resolve(userPack);

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

            var result = resolver.Resolve(userPack, tempDir);

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

        var act = () => resolver.Resolve(userPack);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("Run 'ariadne sync'");
    }

    [Fact]
    public void Resolve_ImportProtocol_NoCacheDir_ShouldThrow()
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

            var act = () => resolver.Resolve(userPack, tempDir);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*sync*");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
