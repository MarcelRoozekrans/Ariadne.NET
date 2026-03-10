# Architecture Governance Engine

A pluggable, pattern-agnostic architecture governance engine that enforces structural rules for AI code generation via declarative YAML rule packs.

> **Core Principle:** AI generates behavior. The engine governs structure.

## Why

AI coding agents generate behavior effectively but are unreliable at structural decisions — they invent folder structures, improvise namespaces, and silently violate dependency rules. This engine separates structural governance from AI code generation: the AI declares intent, the engine enforces structure.

## Quick Start

### CLI

```bash
# Initialize a project with clean architecture rules
dotnet run --project src/ArchGov.Host.Cli -- init --template clean-architecture

# Compose multiple patterns
dotnet run --project src/ArchGov.Host.Cli -- init --template clean-architecture+cqrs

# List available component roles
dotnet run --project src/ArchGov.Host.Cli -- roles

# Show resolved architecture rules (layers + dependencies)
dotnet run --project src/ArchGov.Host.Cli -- rules

# Generate a component
dotnet run --project src/ArchGov.Host.Cli -- generate \
  --role Entity --name Order --solution MyApp --feature Orders
```

### API

```bash
# Start the API server
dotnet run --project src/ArchGov.Host.Api

# List available templates
curl http://localhost:5000/v1.0/architecture/templates

# Get layer definitions
curl http://localhost:5000/v1.0/architecture/layers

# Get component roles
curl http://localhost:5000/v1.0/architecture/component-roles

# Generate components
curl -X POST http://localhost:5000/v1.0/architecture/generate \
  -H "Content-Type: application/json" \
  -d '{
    "solutionName": "MyApp",
    "components": [
      { "componentRole": "Entity", "name": "Order" },
      { "componentRole": "Repository", "name": "OrderRepository" }
    ]
  }'
```

### MCP Server

The engine runs as an MCP server for AI agent integration (Claude Code, Cursor, etc.):

**Stdio (local)** — add to your `.mcp.json`:

```json
{
  "mcpServers": {
    "archgov": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/ArchGov.Host.Mcp"]
    }
  }
}
```

**HTTP (remote/team)** — start the API server, MCP endpoint at `/mcp`:

```bash
dotnet run --project src/ArchGov.Host.Api
```

**MCP Tools:**

| Tool | Description |
|------|-------------|
| `list_templates` | List available architecture templates |
| `get_layers` | Get layer definitions with allowed dependencies |
| `get_component_roles` | Get available component roles |
| `get_rules` | Get code feature rules |
| `generate_component` | Generate a C# component |
| `validate_source` | Validate source files against rules |
| `lint_source` | Lint for namespace-layer mismatches |

**MCP Resources:**

| Resource | Description |
|----------|-------------|
| `archgov://packs/{name}` | Full YAML content of a built-in rule pack |

All tools auto-detect `.architecture/rules.yaml` in the working directory. Pass an explicit `template` parameter to override.

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

Seven templates ship with the engine:

| Template | Layers | Roles | Description |
|----------|--------|-------|-------------|
| `clean-architecture` | Domain, Application, Infrastructure, WebApi | 19 | Classic Clean Architecture |
| `hexagonal-architecture` | Core, Ports, Adapters | 7 | Ports & Adapters pattern |
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
dotnet run --project src/ArchGov.Host.Cli -- sync

# Force fresh sync (ignore cache)
dotnet run --project src/ArchGov.Host.Cli -- sync --no-cache

# Sync a single import
dotnet run --project src/ArchGov.Host.Cli -- sync --name company-standards
```

Then reference synced packs in your rules:

```yaml
# .architecture/rules.yaml
extends:
  - import://company-standards
  - import://team-conventions
```

Private repos work out of the box — the engine uses your existing git credentials (SSH keys, credential managers). Versions are resolved against git tags using semver ranges (`^2.0`, `~1.3`, or exact `1.0.0`). Packs are cached locally in `.architecture/.cache/` so validation works offline.

### 5. Composability & Merge Semantics

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

### 6. Code Generation

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

### 7. Source Validation

The engine validates existing source code against architecture rules using Roslyn parsing:

```bash
# CLI
dotnet run --project src/ArchGov.Host.Cli -- validate ./src

# API
curl -X POST http://localhost:5000/v1.0/architecture/validate \
  -H "Content-Type: application/json" \
  -d '{"sourceDirectory": "./src", "template": "clean-architecture"}'
```

Checks performed:
- **Dependency validation**: verifies `using` directives comply with layer dependency rules (e.g. Domain cannot reference Infrastructure)
- **Lint**: checks namespace segments match directory-based layer structure

### 8. Lint

A lighter check that detects namespace-vs-directory layer mismatches:

```bash
# CLI
dotnet run --project src/ArchGov.Host.Cli -- lint ./src

# API
curl -X POST http://localhost:5000/v1.0/architecture/lint \
  -H "Content-Type: application/json" \
  -d '{"sourceDirectory": "./src", "template": "clean-architecture"}'
```

## Architecture

```
src/
  ArchGov.Core/              # Rule engine library (zero host dependencies)
    RuleSchema/               # Models: RulePack, LayerDefinition, ComponentRoleDefinition
    RuleLoader/               # YAML parsing, built-in pack registry, resolver
    RuleEngine/               # Merger, dependency validator, layer inference
    CodeGeneration/           # Roslyn-based code generator
    Orchestration/            # Wires inference + validation + generation + workspace
    Validation/               # Source file validation with Roslyn parsing
    Sync/                     # Git-based sync: imports, semver, caching
    BuiltinPacks/             # Embedded YAML templates

  ArchGov.Host.Api/           # ASP.NET REST API + MCP HTTP transport
  ArchGov.Host.Cli/           # CLI tool (System.CommandLine)
  ArchGov.Host.Mcp/           # MCP stdio server for AI agents

tests/
  ArchGov.Core.Tests/         # 168 unit + integration tests
  ArchGov.Host.Mcp.Tests/     # 10 MCP tool + resource tests
  ArchGov.Host.Api.Tests/     # 6 integration tests
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
services.AddArchGovCore();
```

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

## API Reference

### REST Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `/v1.0/architecture/templates` | GET | List available built-in templates |
| `/v1.0/architecture/layers` | GET | Get layer definitions (query: `?template=`) |
| `/v1.0/architecture/component-roles` | GET | Get component roles (query: `?template=`) |
| `/v1.0/architecture/rules` | GET | Get code feature rules (query: `?template=`) |
| `/v1.0/architecture/generate` | POST | Generate component code |
| `/v1.0/architecture/generate-workspace` | POST | Scaffold solution project structure |
| `/v1.0/architecture/validate` | POST | Validate source files against rules |
| `/v1.0/architecture/lint` | POST | Lint source files for layer mismatches |

### CLI Commands

| Command | Description |
|---------|-------------|
| `arch init --template <name>` | Initialize `.architecture/rules.yaml` |
| `arch rules` | Print resolved layer rules |
| `arch roles` | Print component roles table |
| `arch generate --role <role> --name <name> --solution <sol> --feature <feat>` | Generate a component |
| `arch validate <path>` | Validate source files against architecture rules |
| `arch lint <path>` | Lint source files for namespace-layer mismatches |
| `arch sync` | Sync rule packs from git repositories defined in imports.yaml |

## Building

```bash
dotnet build src/ArchGov.slnx
dotnet test src/ArchGov.slnx
```

## Tech Stack

- **.NET 10**
- **YamlDotNet** — YAML parsing
- **Microsoft.CodeAnalysis.CSharp** (Roslyn) — Code generation
- **System.CommandLine** — CLI framework
- **ASP.NET Core** — API host with API versioning + Swagger
- **ModelContextProtocol** — MCP server SDK (stdio + HTTP)
- **xUnit + FluentAssertions** — Testing
