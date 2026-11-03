# Ariadne.NET

[![CI](https://github.com/MarcelRoozekrans/Ariadne.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/MarcelRoozekrans/Ariadne.NET/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/MarcelRoozekrans/Ariadne.NET/graph/badge.svg)](https://codecov.io/gh/MarcelRoozekrans/Ariadne.NET)
[![Ariadne.NET](https://img.shields.io/nuget/v/Ariadne.NET?label=Ariadne.NET)](https://www.nuget.org/packages/Ariadne.NET)
[![Ariadne.NET.Abstractions](https://img.shields.io/nuget/v/Ariadne.NET.Abstractions?label=Ariadne.NET.Abstractions)](https://www.nuget.org/packages/Ariadne.NET.Abstractions)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://marcelroozekrans.github.io/Ariadne.NET/)

A .NET library that enforces structural rules on AI-generated code. You describe an architecture in a declarative YAML rule pack: layers, allowed dependencies, component roles and naming. Ariadne.NET resolves the pack, generates correctly placed C# components, and checks existing source against it.

AI coding agents write behavior well but invent folder structures, improvise namespaces and break dependency rules without saying so. Here the AI declares intent and the library decides the structure.

## Install

```bash
dotnet add package Ariadne.NET
```

`Ariadne.NET.Abstractions` holds the rule-pack schema and contracts. Reference it alone when your tool only needs to describe rule packs.

Load a rule pack and resolve it in a console app. This needs `dotnet add package Microsoft.Extensions.DependencyInjection` as well, because Ariadne.NET references only the dependency injection abstractions.

```csharp
using Ariadne.NET.Abstractions;
using Microsoft.Extensions.DependencyInjection;

using var services = new ServiceCollection().AddAriadneNETServices().BuildServiceProvider();
var loader = services.GetRequiredService<IRulePackLoader>();
var resolver = services.GetRequiredService<IRulePackResolver>();

// A rule pack that builds on the built-in clean-architecture pack.
var pack = loader.LoadFromString("""
    name: my-service
    extends:
      - builtin://clean-architecture
    """);
if (pack.IsFailure)
{
    Console.Error.WriteLine(pack.Error.Message);
    return 1;
}

var resolved = resolver.Resolve(pack.Value);
if (resolved.IsFailure)
{
    Console.Error.WriteLine(resolved.Error.Message);
    return 1;
}

Console.WriteLine($"{resolved.Value.Name}: {resolved.Value.Layers.Count} layers, {resolved.Value.ComponentRoles.Count} component roles");
foreach (var layer in resolved.Value.Layers)
{
    Console.WriteLine($"  {layer.Name} -> [{string.Join(", ", layer.AllowedDependencies)}]");
}

return 0;
```

`dotnet run` prints the resolved pack name, its layer and component role counts, and each layer with the layers it may depend on. The [documentation site](https://marcelroozekrans.github.io/Ariadne.NET/) has the full guide.

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
| `validate_source` | Validate source files against rules |
| `lint_source` | Lint for namespace-layer mismatches |

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

### 6. Source Validation

The engine validates existing source code against architecture rules using Roslyn parsing:

```bash
# CLI
dotnet run --project src/Ariadne.NET.Cli -- validate ./src
```

Over MCP, use the `validate_source` tool.

Checks performed:
- **Dependency validation**: verifies `using` directives comply with layer dependency rules (e.g. Domain cannot reference Infrastructure)
- **Lint**: checks namespace segments match directory-based layer structure

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
    RuleEngine/               # Merger, dependency validator, layer inference
    CodeGeneration/           # Roslyn-based code generator
    Orchestration/            # Wires inference + validation + generation + workspace
    Validation/               # Source file validation with Roslyn parsing
    Sync/                     # Git-based sync: imports, semver, caching
    BuiltinPacks/             # Embedded YAML templates

  Ariadne.NET.Cli/            # CLI tool (System.CommandLine)
  Ariadne.NET.Mcp/            # MCP stdio server for AI agents
  Ariadne.NET.Mcp.Http/       # MCP HTTP transport (reuses the Ariadne.NET.Mcp tools)

tests/
  Ariadne.NET.Tests/          # Unit + integration tests
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
        -> DependencyValidator (checks layer dependencies)
          -> CodeGeneratorService (Roslyn code output)
```

### DI Registration

All core services are registered with a single call:

```csharp
services.AddAriadneNETServices();
```

The method is composed from the registrations that ZeroAlloc.Inject generates at compile time from the `[Singleton]` attribute on each service class, plus the ZeroAlloc.Resilience-wrapped git client.

This registers:
| Interface | Implementation | Lifetime |
|-----------|---------------|----------|
| `IRulePackLoader` | `YamlRulePackLoader` | Singleton |
| `IBuiltinPackRegistry` | `BuiltinPackRegistry` | Singleton |
| `IRulePackMerger` | `RulePackMerger` | Singleton |
| `IRulePackResolver` | `RulePackResolver` | Singleton |
| `IDependencyValidator` | `DependencyValidator` | Singleton |
| `ILayerInferenceEngine` | `LayerInferenceEngine` | Singleton |
| `ICodeGeneratorService` | `CodeGeneratorService` | Singleton |
| `IGenerateOrchestrator` | `GenerateOrchestrator` | Singleton |
| `IWorkspaceOrchestrator` | `WorkspaceOrchestrator` | Singleton |
| `ISourceValidator` | `SourceValidator` | Singleton |
| `ILinter` | `Linter` | Singleton |
| `IImportsLoader` | `ImportsYamlLoader` | Singleton |
| `IGitSyncProvider` | `GitCliSyncProvider` | Singleton |
| `ISyncOrchestrator` | `SyncOrchestrator` | Singleton |

## CLI Reference

| Command | Description |
|---------|-------------|
| `ariadne init --template <name>` | Initialize `.architecture/rules.yaml` |
| `ariadne rules` | Print resolved layer rules |
| `ariadne roles` | Print component roles table |
| `ariadne generate --role <role> --name <name> --solution <sol> --feature <feat>` | Generate a component |
| `ariadne validate <path>` | Validate source files against architecture rules |
| `ariadne lint <path>` | Lint source files for namespace-layer mismatches |
| `ariadne sync` | Sync rule packs from git repositories defined in imports.yaml |

Exit codes: `0` success, `1` violations or lint issues found, `2` error (missing files or directories, failed generation or sync). CI pipelines can fail on any non-zero code, or treat `1` and `2` differently.

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

[MIT](LICENSE)
