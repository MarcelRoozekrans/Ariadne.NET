# Architecture Governance Engine — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a pluggable, pattern-agnostic architecture governance engine that enforces structural rules for AI code generation via declarative YAML rule packs.

**Architecture:** .NET 10 solution with a Core library (rule schema, engine, YAML loading, code generation), built-in YAML packs as embedded resources, an ASP.NET Host.Api for MCP consumption, and a Host.Cli for local developer/CI use. Follows Clean Architecture internally — dogfooding itself.

**Tech Stack:** .NET 10, YamlDotNet (YAML parsing), Microsoft.CodeAnalysis.CSharp (Roslyn code gen), System.CommandLine (CLI), ASP.NET Core (API), xUnit + FluentAssertions (testing)

**Design doc:** `docs/plans/2026-03-10-architecture-governance-engine-design.md`

---

## Task 1: Solution Scaffold

**Files:**
- Create: `src/ArchGov.sln`
- Create: `src/ArchGov.Core/ArchGov.Core.csproj`
- Create: `src/ArchGov.Host.Api/ArchGov.Host.Api.csproj`
- Create: `src/ArchGov.Host.Cli/ArchGov.Host.Cli.csproj`
- Create: `tests/ArchGov.Core.Tests/ArchGov.Core.Tests.csproj`
- Create: `tests/ArchGov.Host.Api.Tests/ArchGov.Host.Api.Tests.csproj`

**Step 1: Create solution and projects**

```bash
mkdir -p src tests
cd src
dotnet new sln -n ArchGov
dotnet new classlib -n ArchGov.Core -f net10.0
dotnet new webapi -n ArchGov.Host.Api -f net10.0 --no-openapi
dotnet new console -n ArchGov.Host.Cli -f net10.0
cd ../tests
dotnet new xunit -n ArchGov.Core.Tests -f net10.0
dotnet new xunit -n ArchGov.Host.Api.Tests -f net10.0
```

**Step 2: Wire project references**

```bash
cd ../src
dotnet sln add ArchGov.Core ArchGov.Host.Api ArchGov.Host.Cli ../tests/ArchGov.Core.Tests ../tests/ArchGov.Host.Api.Tests
dotnet add ArchGov.Host.Api reference ArchGov.Core
dotnet add ArchGov.Host.Cli reference ArchGov.Core
cd ../tests
dotnet add ArchGov.Core.Tests reference ../src/ArchGov.Core
dotnet add ArchGov.Host.Api.Tests reference ../src/ArchGov.Host.Api ../src/ArchGov.Core
```

**Step 3: Add NuGet packages**

```bash
cd ../src
dotnet add ArchGov.Core package YamlDotNet
dotnet add ArchGov.Core package Microsoft.CodeAnalysis.CSharp
dotnet add ArchGov.Core package Microsoft.CodeAnalysis.CSharp.Workspaces
dotnet add ArchGov.Host.Cli package System.CommandLine --prerelease
dotnet add ArchGov.Host.Api package Asp.Versioning.Mvc
dotnet add ArchGov.Host.Api package Asp.Versioning.Mvc.ApiExplorer
dotnet add ArchGov.Host.Api package Swashbuckle.AspNetCore
cd ../tests
dotnet add ArchGov.Core.Tests package FluentAssertions
dotnet add ArchGov.Host.Api.Tests package FluentAssertions
dotnet add ArchGov.Host.Api.Tests package Microsoft.AspNetCore.Mvc.Testing
```

**Step 4: Delete auto-generated placeholder files**

Remove `Class1.cs` from Core, `Program.cs` boilerplate from Cli (we'll write our own).

**Step 5: Verify build**

Run: `dotnet build src/ArchGov.sln`
Expected: Build succeeded with 0 errors

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: scaffold solution with Core, Host.Api, Host.Cli, and test projects"
```

---

## Task 2: Rule Schema Models

Define the core data models that represent a rule pack in C#. These mirror the YAML schema from the design doc.

**Files:**
- Create: `src/ArchGov.Core/RuleSchema/RulePack.cs`
- Create: `src/ArchGov.Core/RuleSchema/LayerDefinition.cs`
- Create: `src/ArchGov.Core/RuleSchema/ComponentRoleDefinition.cs`
- Create: `src/ArchGov.Core/RuleSchema/CodeFeatureRuleDefinition.cs`
- Create: `src/ArchGov.Core/RuleSchema/NamingConventions.cs`
- Create: `src/ArchGov.Core/RuleSchema/CSharpTypeKind.cs`
- Test: `tests/ArchGov.Core.Tests/RuleSchema/RulePackTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/ArchGov.Core.Tests/RuleSchema/RulePackTests.cs
using FluentAssertions;
using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.Tests.RuleSchema;

public class RulePackTests
{
    [Fact]
    public void RulePack_ShouldHoldLayersAndRoles()
    {
        var pack = new RulePack
        {
            Name = "clean-architecture",
            Version = "1.0.0",
            Author = "platform-team",
            Layers =
            [
                new LayerDefinition { Name = "Domain", Description = "Entities", AllowedDependencies = [] },
                new LayerDefinition { Name = "Application", Description = "Use cases", AllowedDependencies = ["Domain"] }
            ],
            ComponentRoles =
            [
                new ComponentRoleDefinition
                {
                    Name = "Entity",
                    Layer = "Domain",
                    TypeKind = CSharpTypeKind.Class,
                    RequiresInterface = false
                }
            ],
            NamingConventions = new NamingConventions
            {
                Namespace = "{SolutionName}.{Feature}.{Layer}",
                FilePath = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs"
            }
        };

        pack.Name.Should().Be("clean-architecture");
        pack.Layers.Should().HaveCount(2);
        pack.Layers[1].AllowedDependencies.Should().Contain("Domain");
        pack.ComponentRoles.Should().HaveCount(1);
        pack.ComponentRoles[0].TypeKind.Should().Be(CSharpTypeKind.Class);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/ArchGov.Core.Tests --filter RulePackTests -v n`
Expected: FAIL — types don't exist yet

**Step 3: Implement the models**

```csharp
// src/ArchGov.Core/RuleSchema/CSharpTypeKind.cs
namespace ArchGov.Core.RuleSchema;

public enum CSharpTypeKind
{
    Class,
    Interface,
    Record,
    Struct,
    RecordStruct
}
```

```csharp
// src/ArchGov.Core/RuleSchema/LayerDefinition.cs
namespace ArchGov.Core.RuleSchema;

public sealed class LayerDefinition
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public List<string> AllowedDependencies { get; init; } = [];
}
```

```csharp
// src/ArchGov.Core/RuleSchema/ComponentRoleDefinition.cs
namespace ArchGov.Core.RuleSchema;

public sealed class ComponentRoleDefinition
{
    public required string Name { get; init; }
    public required string Layer { get; init; }
    public CSharpTypeKind TypeKind { get; init; } = CSharpTypeKind.Class;
    public bool RequiresInterface { get; init; }
    public string? InterfaceRole { get; init; }
    public string Accessibility { get; init; } = "public";
    public string Description { get; init; } = "";
    public List<string> AlternativeNames { get; init; } = [];
}
```

```csharp
// src/ArchGov.Core/RuleSchema/CodeFeatureRuleDefinition.cs
namespace ArchGov.Core.RuleSchema;

public sealed class CodeFeatureRuleDefinition
{
    public required string Id { get; init; }
    public required string Description { get; init; }
    public Dictionary<string, object> AppliesTo { get; init; } = [];
    public bool Locked { get; init; }
}
```

```csharp
// src/ArchGov.Core/RuleSchema/NamingConventions.cs
namespace ArchGov.Core.RuleSchema;

public sealed class NamingConventions
{
    public string Namespace { get; init; } = "{SolutionName}.{Feature}.{Layer}";
    public string FilePath { get; init; } = "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs";
}
```

```csharp
// src/ArchGov.Core/RuleSchema/RulePack.cs
namespace ArchGov.Core.RuleSchema;

public sealed class RulePack
{
    public required string Name { get; init; }
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = "";
    public List<string> Extends { get; init; } = [];
    public List<LayerDefinition> Layers { get; init; } = [];
    public List<ComponentRoleDefinition> ComponentRoles { get; init; } = [];
    public List<CodeFeatureRuleDefinition> CodeFeatureRules { get; init; } = [];
    public NamingConventions NamingConventions { get; init; } = new();
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/ArchGov.Core.Tests --filter RulePackTests -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add src/ArchGov.Core/RuleSchema tests/ArchGov.Core.Tests/RuleSchema
git commit -m "feat: add rule schema models (RulePack, LayerDefinition, ComponentRoleDefinition)"
```

---

## Task 3: YAML Rule Pack Loader

Parse YAML files into `RulePack` instances using YamlDotNet.

**Files:**
- Create: `src/ArchGov.Core/RuleLoader/IRulePackLoader.cs`
- Create: `src/ArchGov.Core/RuleLoader/YamlRulePackLoader.cs`
- Test: `tests/ArchGov.Core.Tests/RuleLoader/YamlRulePackLoaderTests.cs`
- Create: `tests/ArchGov.Core.Tests/RuleLoader/TestData/simple-pack.yaml`

**Step 1: Write the failing test**

```csharp
// tests/ArchGov.Core.Tests/RuleLoader/YamlRulePackLoaderTests.cs
using FluentAssertions;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.Tests.RuleLoader;

public class YamlRulePackLoaderTests
{
    [Fact]
    public void LoadFromString_ShouldParseValidYaml()
    {
        var yaml = """
            name: test-pack
            version: "1.0.0"
            layers:
              - name: Domain
                description: "Core domain"
                allowedDependencies: []
              - name: Application
                description: "Use cases"
                allowedDependencies:
                  - Domain
            componentRoles:
              - name: Entity
                layer: Domain
                typeKind: Class
                requiresInterface: false
              - name: Repository
                layer: Infrastructure
                typeKind: Class
                requiresInterface: true
                interfaceRole: RepositoryInterface
                accessibility: internal
            namingConventions:
              namespace: "{SolutionName}.{Feature}.{Layer}"
              filePath: "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs"
            """;

        var loader = new YamlRulePackLoader();
        var pack = loader.LoadFromString(yaml);

        pack.Name.Should().Be("test-pack");
        pack.Layers.Should().HaveCount(2);
        pack.Layers[1].AllowedDependencies.Should().Contain("Domain");
        pack.ComponentRoles.Should().HaveCount(2);
        pack.ComponentRoles[1].RequiresInterface.Should().BeTrue();
        pack.ComponentRoles[1].Accessibility.Should().Be("internal");
    }

    [Fact]
    public void LoadFromFile_ShouldReadAndParseYamlFile()
    {
        var testDataPath = Path.Combine(
            AppContext.BaseDirectory, "RuleLoader", "TestData", "simple-pack.yaml");

        var loader = new YamlRulePackLoader();
        var pack = loader.LoadFromFile(testDataPath);

        pack.Name.Should().Be("simple-test");
        pack.Layers.Should().NotBeEmpty();
    }
}
```

Create test data file:
```yaml
# tests/ArchGov.Core.Tests/RuleLoader/TestData/simple-pack.yaml
name: simple-test
version: "1.0.0"
layers:
  - name: Domain
    description: "Domain layer"
    allowedDependencies: []
```

Mark the test data as `CopyToOutputDirectory: PreserveNewest` in the `.csproj`.

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/ArchGov.Core.Tests --filter YamlRulePackLoaderTests -v n`
Expected: FAIL — types don't exist

**Step 3: Implement**

```csharp
// src/ArchGov.Core/RuleLoader/IRulePackLoader.cs
using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleLoader;

public interface IRulePackLoader
{
    RulePack LoadFromString(string yaml);
    RulePack LoadFromFile(string filePath);
}
```

```csharp
// src/ArchGov.Core/RuleLoader/YamlRulePackLoader.cs
using ArchGov.Core.RuleSchema;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchGov.Core.RuleLoader;

public sealed class YamlRulePackLoader : IRulePackLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public RulePack LoadFromString(string yaml)
    {
        return _deserializer.Deserialize<RulePack>(yaml);
    }

    public RulePack LoadFromFile(string filePath)
    {
        var yaml = File.ReadAllText(filePath);
        return LoadFromString(yaml);
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/ArchGov.Core.Tests --filter YamlRulePackLoaderTests -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add src/ArchGov.Core/RuleLoader tests/ArchGov.Core.Tests/RuleLoader
git commit -m "feat: add YAML rule pack loader with YamlDotNet"
```

---

## Task 4: Rule Pack Merger (Composability)

Merge multiple rule packs into a single resolved pack. Handles `extends` and layered composition.

**Files:**
- Create: `src/ArchGov.Core/RuleEngine/IRulePackMerger.cs`
- Create: `src/ArchGov.Core/RuleEngine/RulePackMerger.cs`
- Test: `tests/ArchGov.Core.Tests/RuleEngine/RulePackMergerTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/ArchGov.Core.Tests/RuleEngine/RulePackMergerTests.cs
using FluentAssertions;
using ArchGov.Core.RuleEngine;
using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.Tests.RuleEngine;

public class RulePackMergerTests
{
    [Fact]
    public void Merge_ShouldCombineLayersFromMultiplePacks()
    {
        var basePack = new RulePack
        {
            Name = "base",
            Layers = [new LayerDefinition { Name = "Domain", AllowedDependencies = [] }]
        };
        var extensionPack = new RulePack
        {
            Name = "extension",
            Layers = [new LayerDefinition { Name = "Application", AllowedDependencies = ["Domain"] }]
        };

        var merger = new RulePackMerger();
        var result = merger.Merge([basePack, extensionPack]);

        result.Layers.Should().HaveCount(2);
        result.Layers.Select(l => l.Name).Should().Contain("Domain").And.Contain("Application");
    }

    [Fact]
    public void Merge_ShouldUnionDependenciesForDuplicateLayers()
    {
        var pack1 = new RulePack
        {
            Name = "pack1",
            Layers = [new LayerDefinition { Name = "Infra", AllowedDependencies = ["Domain"] }]
        };
        var pack2 = new RulePack
        {
            Name = "pack2",
            Layers = [new LayerDefinition { Name = "Infra", AllowedDependencies = ["Application"] }]
        };

        var merger = new RulePackMerger();
        var result = merger.Merge([pack1, pack2]);

        result.Layers.Should().HaveCount(1);
        result.Layers[0].AllowedDependencies.Should().Contain("Domain").And.Contain("Application");
    }

    [Fact]
    public void Merge_ShouldCombineComponentRoles()
    {
        var pack1 = new RulePack
        {
            Name = "pack1",
            ComponentRoles = [new ComponentRoleDefinition { Name = "Entity", Layer = "Domain" }]
        };
        var pack2 = new RulePack
        {
            Name = "pack2",
            ComponentRoles = [new ComponentRoleDefinition { Name = "Command", Layer = "Application", TypeKind = CSharpTypeKind.Record }]
        };

        var merger = new RulePackMerger();
        var result = merger.Merge([pack1, pack2]);

        result.ComponentRoles.Should().HaveCount(2);
    }

    [Fact]
    public void Merge_LaterPackOverridesDuplicateRole()
    {
        var pack1 = new RulePack
        {
            Name = "pack1",
            ComponentRoles = [new ComponentRoleDefinition { Name = "Entity", Layer = "Domain", TypeKind = CSharpTypeKind.Class }]
        };
        var pack2 = new RulePack
        {
            Name = "pack2",
            ComponentRoles = [new ComponentRoleDefinition { Name = "Entity", Layer = "Domain", TypeKind = CSharpTypeKind.Record }]
        };

        var merger = new RulePackMerger();
        var result = merger.Merge([pack1, pack2]);

        result.ComponentRoles.Should().HaveCount(1);
        result.ComponentRoles[0].TypeKind.Should().Be(CSharpTypeKind.Record);
    }

    [Fact]
    public void Merge_LastNamingConventionsWin()
    {
        var pack1 = new RulePack
        {
            Name = "pack1",
            NamingConventions = new NamingConventions { Namespace = "old" }
        };
        var pack2 = new RulePack
        {
            Name = "pack2",
            NamingConventions = new NamingConventions { Namespace = "new" }
        };

        var merger = new RulePackMerger();
        var result = merger.Merge([pack1, pack2]);

        result.NamingConventions.Namespace.Should().Be("new");
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/ArchGov.Core.Tests --filter RulePackMergerTests -v n`
Expected: FAIL

**Step 3: Implement**

```csharp
// src/ArchGov.Core/RuleEngine/IRulePackMerger.cs
using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleEngine;

public interface IRulePackMerger
{
    RulePack Merge(IReadOnlyList<RulePack> packs);
}
```

```csharp
// src/ArchGov.Core/RuleEngine/RulePackMerger.cs
using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.RuleEngine;

public sealed class RulePackMerger : IRulePackMerger
{
    public RulePack Merge(IReadOnlyList<RulePack> packs)
    {
        if (packs.Count == 0)
            throw new ArgumentException("At least one rule pack is required.", nameof(packs));

        if (packs.Count == 1)
            return packs[0];

        var layers = MergeLayers(packs);
        var roles = MergeRoles(packs);
        var rules = MergeCodeFeatureRules(packs);
        var naming = packs.Last().NamingConventions;

        return new RulePack
        {
            Name = string.Join("+", packs.Select(p => p.Name)),
            Version = packs.Last().Version,
            Layers = layers,
            ComponentRoles = roles,
            CodeFeatureRules = rules,
            NamingConventions = naming
        };
    }

    private static List<LayerDefinition> MergeLayers(IReadOnlyList<RulePack> packs)
    {
        var merged = new Dictionary<string, LayerDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in packs.SelectMany(p => p.Layers))
        {
            if (merged.TryGetValue(layer.Name, out var existing))
            {
                var unionDeps = existing.AllowedDependencies
                    .Union(layer.AllowedDependencies, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                merged[layer.Name] = new LayerDefinition
                {
                    Name = layer.Name,
                    Description = layer.Description.Length > 0 ? layer.Description : existing.Description,
                    AllowedDependencies = unionDeps
                };
            }
            else
            {
                merged[layer.Name] = layer;
            }
        }

        return merged.Values.ToList();
    }

    private static List<ComponentRoleDefinition> MergeRoles(IReadOnlyList<RulePack> packs)
    {
        var merged = new Dictionary<string, ComponentRoleDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in packs.SelectMany(p => p.ComponentRoles))
        {
            merged[role.Name] = role; // later wins
        }

        return merged.Values.ToList();
    }

    private static List<CodeFeatureRuleDefinition> MergeCodeFeatureRules(IReadOnlyList<RulePack> packs)
    {
        var merged = new Dictionary<string, CodeFeatureRuleDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in packs.SelectMany(p => p.CodeFeatureRules))
        {
            merged[rule.Id] = rule;
        }

        return merged.Values.ToList();
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/ArchGov.Core.Tests --filter RulePackMergerTests -v n`
Expected: PASS

**Step 5: Commit**

```bash
git add src/ArchGov.Core/RuleEngine tests/ArchGov.Core.Tests/RuleEngine
git commit -m "feat: add rule pack merger with layer union and role override semantics"
```

---

## Task 5: Built-in Pack Registry + Resolver

Load built-in packs (embedded YAML resources) and resolve `extends: [builtin://clean-architecture]`.

**Files:**
- Create: `src/ArchGov.Core/BuiltinPacks/clean-architecture.yaml`
- Create: `src/ArchGov.Core/BuiltinPacks/hexagonal-architecture.yaml`
- Create: `src/ArchGov.Core/BuiltinPacks/cqrs.yaml`
- Create: `src/ArchGov.Core/RuleLoader/IBuiltinPackRegistry.cs`
- Create: `src/ArchGov.Core/RuleLoader/BuiltinPackRegistry.cs`
- Create: `src/ArchGov.Core/RuleLoader/RulePackResolver.cs`
- Create: `src/ArchGov.Core/RuleLoader/IRulePackResolver.cs`
- Test: `tests/ArchGov.Core.Tests/RuleLoader/BuiltinPackRegistryTests.cs`
- Test: `tests/ArchGov.Core.Tests/RuleLoader/RulePackResolverTests.cs`

**Step 1: Create the 3 built-in YAML packs**

Create `clean-architecture.yaml`, `hexagonal-architecture.yaml`, and `cqrs.yaml` as embedded resources in the Core project. Each pack should define its layers, component roles, code feature rules, and naming conventions following the schema from Task 2.

Reference the AIResearch.Architecture `ComponentRoleProvider` for the clean-architecture pack — it defines ~40 roles across Domain/Application/Infrastructure/WebApi layers.

The hexagonal pack should define: Core (entities, domain services), Ports (interfaces — driving + driven), Adapters (implementations — primary + secondary).

The cqrs pack should define only the Command/Query roles (composable add-on, no layers).

Mark YAML files as `EmbeddedResource` in `ArchGov.Core.csproj`:
```xml
<ItemGroup>
  <EmbeddedResource Include="BuiltinPacks\*.yaml" />
</ItemGroup>
```

**Step 2: Write failing tests for BuiltinPackRegistry**

```csharp
// tests/ArchGov.Core.Tests/RuleLoader/BuiltinPackRegistryTests.cs
using FluentAssertions;
using ArchGov.Core.RuleLoader;

namespace ArchGov.Core.Tests.RuleLoader;

public class BuiltinPackRegistryTests
{
    [Fact]
    public void GetAvailablePacks_ShouldReturnAllBuiltinPacks()
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var packs = registry.GetAvailablePackNames();

        packs.Should().Contain("clean-architecture");
        packs.Should().Contain("hexagonal-architecture");
        packs.Should().Contain("cqrs");
    }

    [Fact]
    public void GetPack_ShouldReturnCleanArchitecturePack()
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var pack = registry.GetPack("clean-architecture");

        pack.Should().NotBeNull();
        pack.Layers.Should().HaveCount(4);
        pack.ComponentRoles.Should().NotBeEmpty();
    }

    [Fact]
    public void GetPack_UnknownName_ShouldThrow()
    {
        var registry = new BuiltinPackRegistry(new YamlRulePackLoader());
        var act = () => registry.GetPack("nonexistent");

        act.Should().Throw<KeyNotFoundException>();
    }
}
```

**Step 3: Implement BuiltinPackRegistry**

Reads embedded YAML resources from the assembly, parses them, and caches them by name.

**Step 4: Write failing tests for RulePackResolver**

```csharp
// tests/ArchGov.Core.Tests/RuleLoader/RulePackResolverTests.cs
using FluentAssertions;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.RuleEngine;
using ArchGov.Core.RuleSchema;

namespace ArchGov.Core.Tests.RuleLoader;

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
            Extends = ["builtin://clean-architecture", "builtin://cqrs"]
        };

        var resolved = resolver.Resolve(userPack);

        resolved.Layers.Should().NotBeEmpty();
        resolved.ComponentRoles.Select(r => r.Name).Should().Contain("Command");
        resolved.ComponentRoles.Select(r => r.Name).Should().Contain("Entity");
    }
}
```

**Step 5: Implement RulePackResolver**

Resolves `extends` references (builtin:// → BuiltinPackRegistry), collects all packs in order, then delegates to `RulePackMerger`.

**Step 6: Run all tests**

Run: `dotnet test tests/ArchGov.Core.Tests -v n`
Expected: All PASS

**Step 7: Commit**

```bash
git add src/ArchGov.Core/BuiltinPacks src/ArchGov.Core/RuleLoader tests/ArchGov.Core.Tests/RuleLoader
git commit -m "feat: add built-in pack registry, resolver, and 3 YAML packs"
```

---

## Task 6: Dependency Validation Engine

Validate that component dependencies comply with the layer dependency rules defined in the resolved rule pack.

**Files:**
- Create: `src/ArchGov.Core/RuleEngine/IDependencyValidator.cs`
- Create: `src/ArchGov.Core/RuleEngine/DependencyValidator.cs`
- Test: `tests/ArchGov.Core.Tests/RuleEngine/DependencyValidatorTests.cs`

**Step 1: Write failing tests**

Test cases:
- Application depending on Domain → valid
- Domain depending on Infrastructure → violation
- Infrastructure depending on Application + Domain → valid
- Unknown layer → violation

**Step 2: Implement**

`DependencyValidator` takes a resolved `RulePack`, looks up the source layer's `AllowedDependencies`, and returns violations for any dependency not in the allowed list.

**Step 3: Run tests, commit**

```bash
git commit -m "feat: add dependency validation engine"
```

---

## Task 7: Layer Inference Engine

Given a component role name, determine which layer it belongs to from the resolved rule pack.

**Files:**
- Create: `src/ArchGov.Core/RuleEngine/ILayerInferenceEngine.cs`
- Create: `src/ArchGov.Core/RuleEngine/LayerInferenceEngine.cs`
- Create: `src/ArchGov.Core/RuleEngine/LayerInferenceResult.cs`
- Test: `tests/ArchGov.Core.Tests/RuleEngine/LayerInferenceEngineTests.cs`

**Step 1: Write failing tests**

Test cases:
- "Entity" → deterministic match: Domain
- "CommandHandler" → deterministic match: Application
- "Repository" → deterministic match: Infrastructure
- "UnknownRole" → invalid
- Role defined in multiple layers (if exists) → ambiguous with options

**Step 2: Implement**

Looks up the role in the resolved pack's `ComponentRoles`, returns the layer. If not found, tries alternative names. Returns `Ok`, `Ambiguous`, or `Invalid`.

**Step 3: Run tests, commit**

```bash
git commit -m "feat: add layer inference engine"
```

---

## Task 8: Code Generation Service

Generate C# source code from a component definition + resolved rule pack, using Roslyn.

**Files:**
- Create: `src/ArchGov.Core/CodeGeneration/ICodeGeneratorService.cs`
- Create: `src/ArchGov.Core/CodeGeneration/CodeGeneratorService.cs`
- Create: `src/ArchGov.Core/CodeGeneration/CodeGenerationRequest.cs`
- Test: `tests/ArchGov.Core.Tests/CodeGeneration/CodeGeneratorServiceTests.cs`

**Step 1: Write failing tests**

Test cases:
- Generate a Class (Entity) → produces `namespace X; public class Order { }`
- Generate an Interface → produces `public interface IOrderRepository { }`
- Generate a Record → produces `public record CreateOrderCommand;`
- Generate internal class with interface → `internal sealed class OrderRepository : IOrderRepository { }`
- Namespace follows naming convention from rule pack
- File path follows convention from rule pack

**Step 2: Implement**

Port the approach from AIResearch.Architecture's `CSharpCodeGeneratorService` but make it rule-pack-driven:
- Namespace template from `NamingConventions.Namespace`
- TypeKind from `ComponentRoleDefinition.TypeKind`
- Accessibility from `ComponentRoleDefinition.Accessibility`
- Format with Roslyn `Formatter`

**Step 3: Run tests, commit**

```bash
git commit -m "feat: add Roslyn-based code generation service driven by rule pack"
```

---

## Task 9: Orchestrator — The Generate Flow

Wire together inference, validation, and code generation into a single orchestrated flow.

**Files:**
- Create: `src/ArchGov.Core/Orchestration/IGenerateOrchestrator.cs`
- Create: `src/ArchGov.Core/Orchestration/GenerateOrchestrator.cs`
- Create: `src/ArchGov.Core/Orchestration/GenerateRequest.cs`
- Create: `src/ArchGov.Core/Orchestration/GenerateResult.cs`
- Test: `tests/ArchGov.Core.Tests/Orchestration/GenerateOrchestratorTests.cs`

**Step 1: Write failing tests**

Test end-to-end: provide a solution name, feature, and component roles → get back a dictionary of file paths to generated code, all rule-compliant.

**Step 2: Implement**

The orchestrator:
1. Resolves the rule pack (extends + merges)
2. For each component: infer layer, validate dependencies, check interface requirements
3. Generate code for each component
4. Return file path → code dictionary

**Step 3: Run tests, commit**

```bash
git commit -m "feat: add generate orchestrator wiring inference, validation, and code gen"
```

---

## Task 10: DI Registration + Core Public API

Create `ServiceCollectionExtensions` for the Core library so hosts can register all services with one call.

**Files:**
- Create: `src/ArchGov.Core/ServiceCollectionExtensions.cs`
- Test: `tests/ArchGov.Core.Tests/ServiceRegistrationTests.cs`

**Step 1: Write test verifying all services resolve**

**Step 2: Implement `AddArchGovCore(this IServiceCollection services)`**

**Step 3: Run tests, commit**

```bash
git commit -m "feat: add DI registration for Core services"
```

---

## Task 11: ASP.NET Host.Api

Expose the engine as a REST API (MCP-compatible).

**Files:**
- Modify: `src/ArchGov.Host.Api/Program.cs`
- Create: `src/ArchGov.Host.Api/Controllers/ArchitectureController.cs`
- Create: `src/ArchGov.Host.Api/Controllers/TemplatesController.cs`
- Test: `tests/ArchGov.Host.Api.Tests/ArchitectureControllerTests.cs`

**Endpoints to implement:**

| Route | Method | Action |
|-------|--------|--------|
| `GET /v1.0/architecture/layers` | GET | Returns layers from resolved pack |
| `GET /v1.0/architecture/rules` | GET | Returns code feature rules |
| `GET /v1.0/architecture/component-roles` | GET | Returns component roles |
| `GET /v1.0/architecture/templates` | GET | Lists built-in packs |
| `POST /v1.0/architecture/generate` | POST | Generate components |
| `POST /v1.0/architecture/generate-workspace` | POST | Scaffold solution structure |
| `POST /v1.0/architecture/validate` | POST | Validate source files |

**Step 1: Write integration tests using `WebApplicationFactory`**

**Step 2: Implement controllers delegating to Core services**

**Step 3: Configure Program.cs** — API versioning, Swagger, CORS, JSON options, add Core services

**Step 4: Run tests, commit**

```bash
git commit -m "feat: add ASP.NET Host.Api with architecture endpoints"
```

---

## Task 12: CLI Host

Expose the engine as a local CLI tool.

**Files:**
- Modify: `src/ArchGov.Host.Cli/Program.cs`
- Create: `src/ArchGov.Host.Cli/Commands/InitCommand.cs`
- Create: `src/ArchGov.Host.Cli/Commands/GenerateCommand.cs`
- Create: `src/ArchGov.Host.Cli/Commands/ValidateCommand.cs`
- Create: `src/ArchGov.Host.Cli/Commands/RulesCommand.cs`
- Create: `src/ArchGov.Host.Cli/Commands/RolesCommand.cs`

**Step 1: Implement CLI commands using System.CommandLine**

```bash
arch init --template clean-architecture     # creates .architecture/rules.yaml
arch rules                                  # prints resolved rules
arch roles                                  # prints component roles
arch generate --role Entity --name Order --solution MyApp --feature Orders
arch validate ./src
```

**Step 2: Test manually**

Run: `dotnet run --project src/ArchGov.Host.Cli -- init --template clean-architecture`
Expected: `.architecture/rules.yaml` created with clean-architecture pack content

Run: `dotnet run --project src/ArchGov.Host.Cli -- roles`
Expected: Table of component roles printed

**Step 3: Commit**

```bash
git commit -m "feat: add CLI host with init, generate, validate, rules, and roles commands"
```

---

## Task 13: End-to-End Verification

**Step 1: Start the API**

```bash
dotnet run --project src/ArchGov.Host.Api
```

**Step 2: Test API endpoints**

```bash
curl http://localhost:5000/v1.0/architecture/templates
curl http://localhost:5000/v1.0/architecture/layers
curl http://localhost:5000/v1.0/architecture/component-roles
curl -X POST http://localhost:5000/v1.0/architecture/generate \
  -H "Content-Type: application/json" \
  -d '{"solutionName":"MyApp","features":[{"name":"Order"}],"components":[{"componentRole":"Entity","name":"Order"}]}'
```

**Step 3: Test CLI**

```bash
dotnet run --project src/ArchGov.Host.Cli -- init --template clean-architecture+cqrs
dotnet run --project src/ArchGov.Host.Cli -- generate --role CommandHandler --name CreateOrderHandler --solution MyApp --feature Order
```

**Step 4: Run full test suite**

```bash
dotnet test src/ArchGov.sln -v n
```
Expected: All tests PASS

**Step 5: Commit**

```bash
git commit -m "chore: end-to-end verification complete"
```

---

## Summary

| Task | What | Key deliverable |
|------|------|----------------|
| 1 | Solution scaffold | Projects, references, packages |
| 2 | Rule schema models | `RulePack`, `LayerDefinition`, `ComponentRoleDefinition` |
| 3 | YAML loader | `YamlRulePackLoader` |
| 4 | Pack merger | `RulePackMerger` with union/override semantics |
| 5 | Built-in packs + resolver | 3 YAML packs, `BuiltinPackRegistry`, `RulePackResolver` |
| 6 | Dependency validation | `DependencyValidator` |
| 7 | Layer inference | `LayerInferenceEngine` |
| 8 | Code generation | `CodeGeneratorService` (Roslyn) |
| 9 | Orchestrator | `GenerateOrchestrator` wiring it all together |
| 10 | DI registration | `ServiceCollectionExtensions` |
| 11 | API host | REST endpoints (MCP-compatible) |
| 12 | CLI host | `arch init/generate/validate/rules/roles` |
| 13 | E2E verification | Full flow tested |
