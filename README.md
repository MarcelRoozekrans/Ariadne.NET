# Ariadne.NET

[![CI](https://github.com/MarcelRoozekrans/Ariadne.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/MarcelRoozekrans/Ariadne.NET/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/MarcelRoozekrans/Ariadne.NET/graph/badge.svg)](https://codecov.io/gh/MarcelRoozekrans/Ariadne.NET)
[![Ariadne.NET](https://img.shields.io/nuget/v/Ariadne.NET?label=Ariadne.NET)](https://www.nuget.org/packages/Ariadne.NET)
[![Ariadne.NET.Abstractions](https://img.shields.io/nuget/v/Ariadne.NET.Abstractions?label=Ariadne.NET.Abstractions)](https://www.nuget.org/packages/Ariadne.NET.Abstractions)
[![Ariadne.NET.Workspaces](https://img.shields.io/nuget/v/Ariadne.NET.Workspaces?label=Ariadne.NET.Workspaces)](https://www.nuget.org/packages/Ariadne.NET.Workspaces)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://marcelroozekrans.github.io/Ariadne.NET/)

A .NET library that enforces structural rules on AI-generated code. You describe an architecture in a declarative YAML rule pack: layers, allowed dependencies, component roles and naming. Ariadne.NET resolves the pack, generates correctly placed C# components, and checks existing solutions against it.

AI coding agents write behavior well but invent folder structures, improvise namespaces and break dependency rules without saying so. Here the AI declares intent and the library decides the structure.

## Install

```bash
dotnet add package Ariadne.NET.Workspaces
```

`Ariadne.NET.Workspaces` loads solutions and projects with MSBuild and checks them against a rule pack. It brings in `Ariadne.NET`, the rule engine, and `Ariadne.NET.Abstractions`, which holds the rule-pack schema and contracts. Reference `Ariadne.NET` alone to check compilations you build yourself, or `Ariadne.NET.Abstractions` alone when your tool only needs to describe rule packs.

Load a built-in rule pack and check a solution or project against it in a console app. This needs `dotnet add package Microsoft.Extensions.DependencyInjection` as well, because Ariadne.NET references only the dependency injection abstractions. It also needs a .NET SDK on the machine, and one extra reference in the app's project file, which keeps the MSBuild assemblies out of the output. Without it the build fails with `MSBL001`:

```xml
<PackageReference Include="Microsoft.Build.Framework" Version="17.11.48" ExcludeAssets="runtime" />
```

```csharp
using Ariadne.NET.Abstractions;
using Ariadne.NET.Workspaces;
using Microsoft.Extensions.DependencyInjection;

using var services = new ServiceCollection().AddAriadneNETServices().AddAriadneNETWorkspaces().BuildServiceProvider();
var loader = services.GetRequiredService<IRulePackLoader>();
var resolver = services.GetRequiredService<IRulePackResolver>();
var validator = services.GetRequiredService<ISolutionValidator>();

// A rule pack that builds on the built-in clean-architecture pack.
var pack = loader.LoadFromString("""
    name: my-service
    extends:
      - builtin://clean-architecture
    """);
if (pack.IsFailure)
{
    Console.Error.WriteLine(pack.Error.Message);
    return 2;
}

var resolved = resolver.Resolve(pack.Value);
if (resolved.IsFailure)
{
    Console.Error.WriteLine(resolved.Error.Message);
    return 2;
}

Console.WriteLine($"{resolved.Value.Name}: {resolved.Value.Layers.Count} layers, {resolved.Value.ComponentRoles.Count} component roles");
foreach (var layer in resolved.Value.Layers)
{
    Console.WriteLine($"  {layer.Name} -> [{string.Join(", ", layer.AllowedDependencies)}]");
}

// Check a solution or project against the pack: the first argument, else the current directory.
var result = await validator.ValidateAsync(resolved.Value, args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
if (result.IsFailure)
{
    Console.Error.WriteLine(result.Error.Message);
    return 2;
}

var report = result.Value;
Console.WriteLine($"Checked {report.TypesChecked} types: {report.Violations.Count} violations");
foreach (var violation in report.Violations)
{
    var where = violation.Location is { } location ? $"{location.FilePath}:{location.Line}" : violation.SourceType;
    Console.WriteLine($"  [{violation.Kind}] {where}: {violation.Message}");
}

return report.IsValid ? 0 : 1;
```

`dotnet run` prints the resolved pack name, its layer and component role counts, and each layer with the layers it may depend on. It then checks the solution or project you pass as the first argument, or the one in the current directory, and prints each violation with its kind, location and message. The exit code is 1 when there are violations and 2 when the pack or the solution cannot be loaded. Ariadne never restores, so run `dotnet restore` first. The [documentation site](https://marcelroozekrans.github.io/Ariadne.NET/) has the full guide.

## CLI and MCP server

The CLI and the MCP server are not packaged yet. Run them from a clone of this repository.

### CLI

```bash
# Initialize a project with clean architecture rules
dotnet run --project src/Ariadne.NET.Cli -- init --template clean-architecture

# Compose multiple patterns
dotnet run --project src/Ariadne.NET.Cli -- init --template clean-architecture+cqrs

# List available component roles
dotnet run --project src/Ariadne.NET.Cli -- roles

# Show resolved architecture rules (layers + dependencies)
dotnet run --project src/Ariadne.NET.Cli -- rules

# Generate a component
dotnet run --project src/Ariadne.NET.Cli -- generate \
  --role Entity --name Order --solution MyApp --feature Orders

# Check a solution or project against the rules (the path defaults to .)
dotnet run --project src/Ariadne.NET.Cli -- validate MyApp.slnx
```

### MCP Server

The engine runs as an MCP server for AI agent integration (Claude Code, Cursor, etc.):

**Stdio (local)** — add to your `.mcp.json`:

```json
{
  "mcpServers": {
    "ariadne": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/Ariadne.NET.Mcp"]
    }
  }
}
```

**HTTP (remote/team)** — start the API server, MCP endpoint at `/mcp`:

```bash
dotnet run --project src/Ariadne.NET.Mcp.Http
```

**MCP Tools:**

| Tool | Description |
|------|-------------|
| `list_templates` | List available architecture templates |
| `get_layers` | Get layer definitions with allowed dependencies |
| `get_component_roles` | Get available component roles |
| `get_rules` | Get code feature rules |
| `generate_component` | Generate a C# component |
| `generate_workspace` | Scaffold the project structure for a solution |
| `validate_source` | Validate a solution or project (`path`) against rules |
| `lint_source` | Lint a source directory for namespace-layer mismatches |

**MCP Resources:**

| Resource | Description |
|----------|-------------|
| `ariadne://packs/{name}` | Full YAML content of a built-in rule pack |

All tools auto-detect `.architecture/rules.yaml` in the working directory. Pass an explicit `template` parameter to override; composite templates such as `clean-architecture+cqrs` work here as well.

## How It Works

### 1. Declarative Rule Packs

Architecture patterns are defined as YAML rule packs. No code changes needed to support a new pattern.

```yaml
name: clean-architecture
version: "1.0.0"

layers:
  - name: Domain
    description: "Entities, value objects, domain services"
    allowedDependencies: []
  - name: Application
    description: "Use cases, commands, queries"
    allowedDependencies: [Domain]
  - name: Infrastructure
    description: "Repositories, external services"
    allowedDependencies: [Domain, Application]
  - name: WebApi
    description: "Controllers, middleware"
    allowedDependencies: [Domain, Application, Infrastructure]

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
```

### 2. Built-in Templates

Ten templates ship with the library:

| Template | Layers | Roles | Description |
|----------|--------|-------|-------------|
| `clean-architecture` | Domain, Application, Infrastructure, WebApi | 19 | Classic Clean Architecture |
| `onion-architecture` | DomainModel, DomainServices, ApplicationServices, Infrastructure, Presentation | 15 | Onion Architecture (Jeffrey Palermo) |
| `hexagonal-architecture` | Core, Ports, Adapters | 7 | Ports & Adapters pattern |
| `layered` | Presentation, Business, DataAccess | 11 | Traditional N-tier / layered |
| `microservice` | Domain, Application, Infrastructure, Api, Contracts | 17 | Per-service microservice structure |
| `cqrs` | _(composable add-on)_ | 7 | Command/Query separation |
| `ddd-tactical` | _(composable add-on)_ | 9 | DDD tactical patterns (Aggregate, ValueObject, Factory) |
| `event-driven` | Domain, Events, Producers, Consumers, Infrastructure | 9 | Event-driven architecture |
| `vertical-slice` | Feature, SharedKernel, Infrastructure | 8 | Vertical slice / feature-based |
| `modular-monolith` | Module.Public, Module.Core, Module.Infrastructure, Host | 8 | Modular monolith pattern |

Templates are composable — `clean-architecture+cqrs` merges both packs.

### 3. Sharing Rule Packs via Git Sync

Teams can publish rule packs to git repositories and consume them across projects:

```yaml
# .architecture/imports.yaml
imports:
  - name: company-standards
    type: git
    url: git@github.com:acme/architecture-packs.git
    path: packs/clean-architecture
    version: "^2.0"

  - name: team-conventions
    type: git
    url: https://github.com/acme/team-rules.git
    version: "1.3.0"
```

```bash
# Fetch rule packs from git repos
dotnet run --project src/Ariadne.NET.Cli -- sync

# Force fresh sync (ignore cache)
dotnet run --project src/Ariadne.NET.Cli -- sync --no-cache

# Sync a single import
dotnet run --project src/Ariadne.NET.Cli -- sync --name company-standards
```

Then reference synced packs in your rules:

```yaml
# .architecture/rules.yaml
extends:
  - import://company-standards
  - import://team-conventions
```

Private repos work out of the box — the engine uses your existing git credentials (SSH keys, credential managers). Versions are resolved against git tags using semver ranges (`^2.0`, `~1.3`, or exact `1.0.0`). Packs are cached locally in `.architecture/.cache/` so validation works offline.

### 4. Composability & Merge Semantics

Multiple rule packs can be composed together:

```yaml
# .architecture/rules.yaml
extends:
  - builtin://clean-architecture
  - builtin://cqrs

# Team customizations
componentRoles:
  - name: OutboxMessage
    layer: Infrastructure
    typeKind: Class
```

Merge rules:
- **Layers**: merged by name — duplicate names union their `allowedDependencies`
- **Component Roles**: merged by name — later pack wins
- **Code Feature Rules**: merged by ID — **locked rules** in base packs cannot be overridden
- **Naming Conventions**: last pack wins

Use `MergeWithDiagnostics` to get warnings when locked rules are skipped during merging.

### 5. Code Generation

The engine uses Roslyn to generate correctly structured C# code based on rule pack definitions:

```
Input:  role=Entity, name=Order, solution=MyApp, feature=Orders
Output: src/MyApp.Orders.Domain/Order.cs

  namespace MyApp.Orders.Domain;

  public class Order
  {
  }
```

The generated code respects:
- **Type kind** from the role definition (class, interface, record, struct)
- **Accessibility** (public, internal)
- **Interface inheritance** when `requiresInterface` is set
- **Namespace** and **file path** from naming conventions

### 6. Semantic Validation

The engine checks a solution or project against the rules with the compiler's semantic model. `Ariadne.NET.Workspaces` loads the projects with MSBuild, and the engine builds a type graph from each compilation: one edge for every type reference somebody wrote.

```bash
# CLI
dotnet run --project src/Ariadne.NET.Cli -- validate MyApp.slnx
```

```
Checked 2 types in 2 projects
Result: FAIL - 1 violation(s) found.

  [DependencyNotAllowed] Shop.Domain/Order.cs:7
    Layer 'Domain' is not allowed to depend on 'Infrastructure': Shop.Domain.Order references Shop.Infrastructure.SqlStore 3 times
```

Over MCP, use the `validate_source` tool, which takes a `path` and an optional `template` and returns the report.

What counts as a dependency:
- Base types, interfaces, attributes, member signatures, generic arguments and types used in method bodies.
- `var` gives nothing, and neither does an unused `using`. An alias gives the edges its target would give if it were written out, and `using static` members count.
- Generated code is never a source of dependencies, but it can be a target.

Checks performed:
- **Layer dependencies**: a type in one layer may reference a type in another layer only when the rule allows it (for example, Domain cannot reference Infrastructure). A type's layer is the last namespace segment that names a layer. The report holds one violation for each source type and target type, located at the first reference, with the number of references.
- **Component roles**: a type in a role's layer whose name ends with the role name must have the role's type kind and accessibility, and a role that requires an interface needs a matching interface role in the checked projects.

Violations have the kinds `DependencyNotAllowed`, `TypeKindMismatch`, `AccessibilityMismatch` and `MissingInterface`, and the rule ids `layer/<Layer>` and `role/<Role>`. A project that targets several frameworks is checked once per framework, and a violation found in only some of them lists them. The exit code is 0 when clean, 1 for violations and 2 for an error. Ariadne never restores, so run `dotnet restore` first.

### 7. Lint

A lighter check that detects namespace-vs-directory layer mismatches:

```bash
# CLI
dotnet run --project src/Ariadne.NET.Cli -- lint ./src
```

Over MCP, use the `lint_source` tool.

## Architecture

```
src/
  Ariadne.NET.Abstractions/   # Rule-pack schema and contracts (ZeroAlloc.Results and ZeroAlloc.Validation only, AOT)
  Ariadne.NET/                # Rule engine library (zero host dependencies)
    RuleLoader/               # YAML loading, built-in pack registry, extends resolution
    RuleEngine/               # Merger, layer inference
    CodeGeneration/           # Roslyn-based code generator
    Orchestration/            # Wires inference + generation + workspace
    Analysis/                 # Type graph and architecture validator on Roslyn compilations
    Validation/               # Linter (namespace vs directory)
    Sync/                     # Git-based sync: imports, semver, caching
    BuiltinPacks/             # Embedded YAML templates

  Ariadne.NET.Workspaces/     # Loads solutions with MSBuildWorkspace and runs the validator on them
  Ariadne.NET.Cli/            # CLI tool (System.CommandLine)
  Ariadne.NET.Mcp/            # MCP stdio server for AI agents
  Ariadne.NET.Mcp.Http/       # MCP HTTP transport (reuses the Ariadne.NET.Mcp tools)

tests/
  Ariadne.NET.Tests/          # Unit + integration tests
  Ariadne.NET.Workspaces.Tests/ # Solution loading and validation against fixture solutions
  Ariadne.NET.Cli.Tests/      # CLI exit-code tests
  Ariadne.NET.Mcp.Tests/      # MCP tool + resource tests
  Ariadne.NET.AotSmoke/       # Native AOT publish check for Abstractions
```

### Core Pipeline

```
User Request
  -> RulePackResolver (resolves extends, loads built-in packs)
    -> RulePackMerger (combines layers, roles, rules)
      -> LayerInferenceEngine (determines layer from role)
        -> CodeGeneratorService (Roslyn code output)
```

Validation is a separate path: `ISolutionValidator` (Workspaces) loads the solution, then `IArchitectureValidator` builds a type graph per compilation and checks it against the resolved pack.

### DI Registration

All core services are registered with a single call. `Ariadne.NET.Workspaces` adds its own:

```csharp
services.AddAriadneNETServices();
services.AddAriadneNETWorkspaces();
```

`AddAriadneNETWorkspaces()` registers `ISolutionLoader` and `ISolutionValidator`, and the validator needs the core services, so call both. `AddAriadneNETServices()` is composed from the registrations that ZeroAlloc.Inject generates at compile time from the `[Singleton]` attribute on each service class, plus the ZeroAlloc.Resilience-wrapped git client.

The table below lists what is registered:

| Interface | Implementation | Lifetime |
|-----------|---------------|----------|
| `IRulePackLoader` | `YamlRulePackLoader` | Singleton |
| `IBuiltinPackRegistry` | `BuiltinPackRegistry` | Singleton |
| `IRulePackMerger` | `RulePackMerger` | Singleton |
| `IRulePackResolver` | `RulePackResolver` | Singleton |
| `ILayerInferenceEngine` | `LayerInferenceEngine` | Singleton |
| `ICodeGeneratorService` | `CodeGeneratorService` | Singleton |
| `IGenerateOrchestrator` | `GenerateOrchestrator` | Singleton |
| `IWorkspaceOrchestrator` | `WorkspaceOrchestrator` | Singleton |
| `IArchitectureValidator` | `ArchitectureValidator` | Singleton |
| `ILinter` | `Linter` | Singleton |
| `IImportsLoader` | `ImportsYamlLoader` | Singleton |
| `IGitSyncProvider` | `GitCliSyncProvider` | Singleton |
| `ISyncOrchestrator` | `SyncOrchestrator` | Singleton |
| `ISolutionLoader` | `SolutionLoader` | Singleton (Workspaces) |
| `ISolutionValidator` | `SolutionValidator` | Singleton (Workspaces) |

## CLI Reference

| Command | Description |
|---------|-------------|
| `ariadne init --template <name>` | Initialize `.architecture/rules.yaml` |
| `ariadne rules` | Print resolved layer rules |
| `ariadne roles` | Print component roles table |
| `ariadne generate --role <role> --name <name> --solution <sol> --feature <feat>` | Generate a component |
| `ariadne validate [path]` | Validate a solution or project against architecture rules; `path` defaults to `.` |
| `ariadne lint <path>` | Lint source files for namespace-layer mismatches |
| `ariadne sync` | Sync rule packs from git repositories defined in imports.yaml |

Exit codes: `0` success, `1` violations or lint issues found, `2` error (missing files or directories, a solution that does not load, failed generation or sync). CI pipelines can fail on any non-zero code, or treat `1` and `2` differently.

## Building

```bash
dotnet build src/Ariadne.NET.slnx
dotnet test src/Ariadne.NET.slnx
```

## Family

Ariadne.NET is part of a small family of .NET projects for AI work:

- [Minos.NET](https://marcelroozekrans.github.io/Minos.NET/): A client for decision models that returns calibrated, typed answers to typed questions.
- [Rag.NET](https://marcelroozekrans.github.io/Rag.NET/): A modular retrieval-augmented generation pipeline on Microsoft.Extensions.AI.
- [Mnemosyne.NET](https://marcelroozekrans.github.io/Mnemosyne.NET/): Procedural memory for .NET agents: store, find and reuse the procedures that worked.
- [Thalos.NET](https://github.com/MarcelRoozekrans/Thalos.NET): A ZeroAlloc-native agent framework for .NET on Microsoft Agent Framework.
- [Daedalus.NET](https://github.com/MarcelRoozekrans/Daedalus.NET): A .NET 10 runtime where AI executes tasks planned on a task board.

## Built on ZeroAlloc

Ariadne.NET uses these [ZeroAlloc](https://zeroalloc.net) libraries, which are source generators and analyzers with no reflection at runtime:

- [ZeroAlloc.Analyzers](https://analyzers.zeroalloc.net)
- [ZeroAlloc.Inject](https://inject.zeroalloc.net)
- [ZeroAlloc.Resilience](https://resilience.zeroalloc.net)
- [ZeroAlloc.Results](https://results.zeroalloc.net)
- [ZeroAlloc.Telemetry](https://telemetry.zeroalloc.net)
- [ZeroAlloc.Validation](https://validation.zeroalloc.net)

## License

[MIT](https://github.com/MarcelRoozekrans/Ariadne.NET/blob/main/LICENSE)
