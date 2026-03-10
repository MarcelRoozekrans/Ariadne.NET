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

Three templates ship with the engine:

| Template | Layers | Roles | Description |
|----------|--------|-------|-------------|
| `clean-architecture` | Domain, Application, Infrastructure, WebApi | 19 | Classic Clean Architecture |
| `hexagonal-architecture` | Core, Ports, Adapters | 7 | Ports & Adapters pattern |
| `cqrs` | _(composable add-on)_ | 7 | Command/Query separation |

Templates are composable — `clean-architecture+cqrs` merges both packs.

### 3. Composability & Merge Semantics

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
- **Naming Conventions**: last pack wins

### 4. Code Generation

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

### 5. Dependency Validation

The engine validates that component dependencies comply with layer rules. For example, with clean architecture rules:

- `Application` -> `Domain` is valid
- `Domain` -> `Infrastructure` is a violation
- `Infrastructure` -> `Domain`, `Application` is valid

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
    BuiltinPacks/             # Embedded YAML templates

  ArchGov.Host.Api/           # ASP.NET REST API (MCP-compatible)
  ArchGov.Host.Cli/           # CLI tool (System.CommandLine)

tests/
  ArchGov.Core.Tests/         # 40 unit tests
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

### CLI Commands

| Command | Description |
|---------|-------------|
| `arch init --template <name>` | Initialize `.architecture/rules.yaml` |
| `arch rules` | Print resolved layer rules |
| `arch roles` | Print component roles table |
| `arch generate --role <role> --name <name> --solution <sol> --feature <feat>` | Generate a component |
| `arch validate <path>` | Validate source files against architecture rules |

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
- **xUnit + FluentAssertions** — Testing
