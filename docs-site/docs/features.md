---
sidebar_position: 2
---

# Features

Complete feature reference for the Architecture Governance Engine.

## Declarative Rule Packs

Define architecture patterns as YAML — no code changes needed to support new patterns.

- **Layers** with explicit dependency rules (e.g., Domain depends on nothing, Application depends on Domain)
- **Component roles** with type kind (class, interface, record, struct, record struct), accessibility, and interface requirements
- **Naming conventions** with template variables (`{SolutionName}`, `{Feature}`, `{Layer}`, `{ComponentName}`)
- **Code feature rules** with `locked` flag to prevent downstream overrides

```yaml
layers:
  - name: Domain
    allowedDependencies: []
  - name: Application
    allowedDependencies: [Domain]

componentRoles:
  - name: Entity
    layer: Domain
    typeKind: Class
    accessibility: public
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

## Built-in Templates

Ten templates ship out of the box, covering the most common architectural patterns:

| Template | Layers | Roles | Type |
|----------|--------|-------|------|
| `clean-architecture` | Domain, Application, Infrastructure, WebApi | 19 | Standalone |
| `onion-architecture` | DomainModel, DomainServices, ApplicationServices, Infrastructure, Presentation | 15 | Standalone |
| `hexagonal-architecture` | Core, Ports, Adapters | 7 | Standalone |
| `layered` | Presentation, Business, DataAccess | 11 | Standalone |
| `microservice` | Domain, Application, Infrastructure, Api, Contracts | 17 | Standalone |
| `event-driven` | Domain, Events, Producers, Consumers, Infrastructure | 9 | Standalone |
| `vertical-slice` | Feature, SharedKernel, Infrastructure | 8 | Standalone |
| `modular-monolith` | Module.Public, Module.Core, Module.Infrastructure, Host | 8 | Standalone |
| `cqrs` | _(composable)_ | 7 | Add-on |
| `ddd-tactical` | _(composable)_ | 9 | Add-on |

**Standalone** templates define their own layers and can be used independently. **Add-on** templates define only component roles and are designed to be composed with a standalone template: a role-only pack has no layers of its own, so it needs a base pack that declares the layers its roles name (for example Application and Domain). Roles that name an unknown layer are rejected as an Invalid error.

## Composability & Merge Semantics

Multiple rule packs can be composed via the `extends` field:

```yaml
extends:
  - builtin://clean-architecture
  - builtin://cqrs
  - builtin://ddd-tactical
```

Merge rules applied in order (first pack is the base, subsequent packs overlay):

| Element | Merge Strategy |
|---------|---------------|
| Layers | Merged by name (case-insensitive). `allowedDependencies` are unioned. |
| Component Roles | Merged by name. Later pack overrides earlier. |
| Code Feature Rules | Merged by ID. **Locked rules cannot be overridden** — attempts produce a warning. |
| Naming Conventions | Last pack wins. `Namespace` and `FilePath` are rejected when empty. |

Use `MergeWithDiagnostics` programmatically to inspect merge warnings (e.g., locked rule override attempts).

## Roslyn Code Generation

Generates syntactically correct C# files using Roslyn `SyntaxFactory` — no string templates.

**Supported type kinds:** `Class`, `Interface`, `Record`, `Struct`, `RecordStruct`

**Supported accessibility modifiers:** `public`, `internal`, `private`, `protected`, `protected internal`, `private protected`

**Interface inheritance:** When a role has `requiresInterface: true`, the generated class automatically inherits the corresponding interface type.

```
Input:  role=Entity, name=Order, solution=MyApp, feature=Orders
Output: src/MyApp.Orders.Domain/Order.cs

  namespace MyApp.Orders.Domain;

  public class Order
  {
  }
```

## Layer Inference

The engine automatically resolves which layer a component belongs to based on its role name.

- **Primary match**: exact role name (case-insensitive)
- **Alternative names**: roles can declare `alternativeNames` for flexible matching
- **Ambiguity detection**: if a role name matches multiple definitions, the engine returns an `Ambiguous` status with all matching options

## Dependency Validation

Validates that layer dependencies in source code comply with architecture rules by analyzing Roslyn-parsed `using` directives.

Detected violations:
- **DependencyViolation** — a `using` directive references a layer not in `allowedDependencies`
- **TypeKindMismatch** — a type declaration doesn't match the role's expected type kind
- **AccessibilityMismatch** — a type's accessibility modifier doesn't match the role definition
- **MissingInterface** — a role requires an interface but none was found

> Until the `ariadne` dotnet tool ships, run these commands as `dotnet run --project src/Ariadne.NET.Cli -- <command>`.

```bash
# CLI
ariadne validate ./src
```

## Linting

A lighter check that detects namespace-vs-directory layer mismatches without full dependency analysis.

Flags files where the namespace implies one layer but the directory structure implies another — a common issue when files are moved or copied without updating namespaces.

```bash
# CLI
ariadne lint ./src
```

## Workspace Scaffolding

Generates the full project directory and `.csproj` path structure for a solution based on rule pack layers and naming conventions. Available via the `generate_workspace` MCP tool or programmatically via `IWorkspaceOrchestrator`.

Produces paths like:
- `src/MyApp.Orders.Domain/MyApp.Orders.Domain.csproj`
- `src/MyApp.Orders.Application/MyApp.Orders.Application.csproj`
- `src/MyApp.Customers.Domain/MyApp.Customers.Domain.csproj`

## Git Sync

Share and consume rule packs across teams via git repositories.

### Imports manifest

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

### Version resolution

Versions are resolved against git tags using semver ranges:

| Syntax | Meaning |
|--------|---------|
| `1.3.0` | Exact version |
| `^2.0` | Compatible with 2.x (>=2.0.0, <3.0.0) |
| `~1.3` | Approximately 1.3.x (>=1.3.0, <1.4.0) |

### Caching

Packs are cached locally in `.architecture/.cache/git/` so validation works offline. Use `--no-cache` to force a fresh sync.

```bash
ariadne sync                              # Sync all imports
ariadne sync --name company-standards     # Sync a single import
ariadne sync --no-cache                   # Force fresh fetch
```

### Consuming synced packs

```yaml
# .architecture/rules.yaml
extends:
  - import://company-standards
  - import://team-conventions
```

Private repos work out of the box — the engine uses your existing git credentials (SSH keys, credential managers).

## MCP Server Integration

The engine exposes its full capability set as MCP tools and resources for AI agent integration.

### Tools

| Tool | Parameters | Description |
|------|-----------|-------------|
| `list_templates` | — | List available architecture templates |
| `get_layers` | `template?` | Get layer definitions with dependency rules |
| `get_component_roles` | `template?` | Get available component roles |
| `get_rules` | `template?` | Get code feature rules |
| `generate_component` | `role`, `name`, `solution`, `feature`, `template?` | Generate a C# component file |
| `generate_workspace` | `solution`, `features`, `template?` | Scaffold project structure |
| `validate_source` | `sourceDirectory`, `template?` | Validate source against architecture rules |
| `lint_source` | `sourceDirectory`, `template?` | Lint for namespace-layer mismatches |

### Resources

| URI | Description |
|-----|-------------|
| `ariadne://packs/{name}` | Raw YAML content of a built-in rule pack |

### Transports

- **Stdio** — local integration via `.mcp.json` configuration
- **HTTP** — remote/team access via the API server at `/mcp`

All tools auto-detect `.architecture/rules.yaml` in the working directory. Pass `template` to override.

## Three Host Options

| Host | Use Case | Protocol |
|------|----------|----------|
| **CLI** (`Ariadne.NET.Cli`) | Developer workflow, CI/CD pipelines | Terminal |
| **MCP stdio** (`Ariadne.NET.Mcp`) | Local AI agent integration (Claude Code, Cursor) | MCP stdio |
| **MCP HTTP** (`Ariadne.NET.Mcp.Http`) | Remote/team AI agent access | MCP over HTTP |

All three hosts share the same core engine via `services.AddAriadneNETServices()`, composed from registrations that ZeroAlloc.Inject generates at compile time. The two MCP hosts expose identical tools and resources — stdio for local use, HTTP for remote.

## Extensibility

### Custom rule packs

Create your own YAML rule packs with custom layers, roles, and rules. Extend built-in templates or start from scratch:

```yaml
name: my-custom-rules
extends:
  - builtin://clean-architecture

componentRoles:
  - name: OutboxMessage
    layer: Infrastructure
    typeKind: Class
    accessibility: public

codeFeatureRules:
  - id: no-direct-db-access
    description: "Domain layer must not use DbContext directly"
    appliesTo:
      layer: Domain
    locked: true
```

### Locked rules

Mark rules as `locked: true` to enforce organizational standards that cannot be overridden by downstream teams. When a downstream pack attempts to override a locked rule, the merge produces a warning and the original rule is preserved.
