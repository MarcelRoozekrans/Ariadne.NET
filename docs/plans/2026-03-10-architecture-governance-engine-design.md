# Architecture Governance Engine — Design Document

**Date:** 2026-03-10
**Status:** Approved
**Inspired by:** [AIResearch.Architecture](https://github.com/antoine271167/AIResearch.Architecture)

## Context

AI coding agents generate behavior effectively but are unreliable at structural decisions — they invent folder structures, improvise namespaces, and silently violate dependency rules. The AIResearch.Architecture project proved that separating structural governance from AI code generation works: the AI declares intent, the MCP tool enforces structure.

However, that project is hardcoded to Clean Architecture in .NET. This design generalizes the concept into a **pluggable, pattern-agnostic rule engine** where any architecture pattern can be expressed as declarative configuration.

## Core Principle

> AI generates behavior. The engine governs structure.

---

## 1. Declarative Rule Schema

Architecture patterns are defined as YAML rule packs. No code changes needed to support a new pattern.

```yaml
name: "clean-architecture"
version: "1.0.0"
author: "platform-team"

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
  - name: CommandHandler
    layer: Application
    typeKind: Class
    requiresInterface: true
    interfaceRole: CommandHandlerInterface
  - name: Repository
    layer: Infrastructure
    typeKind: Class
    requiresInterface: true
    interfaceRole: RepositoryInterface
    accessibility: internal

codeFeatureRules:
  - id: CODE-001
    description: "Interface-required roles must have a matching interface"
    appliesTo: { requiresInterface: true }
  - id: CODE-002
    description: "Non-interface types with required interfaces must be internal"
    appliesTo: { requiresInterface: true, isInterface: false }

namingConventions:
  namespace: "{SolutionName}.{Feature}.{Layer}"
  filePath: "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs"
```

---

## 2. Built-in Rule Pack Library

Shipped with the engine (10 packs) — users start immediately without writing YAML:

| Pack | Description | Status |
|------|-------------|--------|
| `clean-architecture` | Domain/Application/Infrastructure/WebApi layers (19 roles) | Shipped |
| `hexagonal-architecture` | Ports & adapters, primary/secondary (7 roles) | Shipped |
| `cqrs` | Command/query separation, composable add-on (7 roles) | Shipped |
| `ddd-tactical` | Aggregates, value objects, domain events, composable add-on (9 roles) | Shipped |
| `event-driven` | Event producers/consumers, message handlers (9 roles) | Shipped |
| `vertical-slice` | Feature-based organization (8 roles) | Shipped |
| `modular-monolith` | Module boundaries, internal/public APIs (8 roles) | Shipped |
| `onion-architecture` | Five concentric layers: DomainModel → DomainServices → ApplicationServices → Infrastructure / Presentation (15 roles) | Shipped |
| `layered` | Classic three-tier: Presentation → Business → DataAccess with strict top-down dependencies (11 roles) | Shipped |
| `microservice` | Domain/Application/Infrastructure/Api/Contracts with isolated Contracts layer for cross-service communication (17 roles) | Shipped |

### Initialization

```bash
arch init --template clean-architecture
arch init --template clean-architecture+cqrs+ddd-tactical
```

### Composability

Packs can extend and compose. `cqrs` adds Command/Query roles on top of any base pattern. `ddd-tactical` adds Aggregate, ValueObject, DomainEvent roles.

```yaml
# .architecture/rules.yaml
extends:
  - builtin://clean-architecture
  - builtin://cqrs
  - builtin://ddd-tactical

# Team customizations
componentRoles:
  - name: OutboxMessage
    layer: Infrastructure
    typeKind: Class
    requiresInterface: false
```

### Merge behavior for composed packs

- **Layers:** merged by name — duplicate names union their allowedDependencies
- **Roles:** merged by name — later pack wins (with warning)
- **Rules:** all rules from all packs apply — conflicts resolved explicitly via overrides

---

## 3. Local-First with Optional Sync

### Local-first (default)

```
Developer clones repo
  → .architecture/ folder is already there
  → Engine reads rules.yaml, done
  → No server, no account, no setup
```

### Project structure

```
.architecture/
  rules.yaml          # active rules (extends built-ins + team customizations)
  imports.yaml         # references to shared rule packs
  overrides.yaml       # local overrides (must be allowed by org rules)
```

### Sharing rules across teams

```yaml
# .architecture/imports.yaml
sources:
  - name: org-standards
    type: git
    url: git@github.com:myorg/architecture-standards.git
    path: packs/dotnet-microservice.yaml
    version: "^2.0"

  - name: platform-baseline
    type: nuget
    package: MyOrg.Architecture.Standards
    version: "1.3.0"
```

### Sync providers

| Method | Complexity | Best for |
|--------|-----------|----------|
| **Git repo** | Zero infra | Small orgs, everything in git already |
| **NuGet feed** | Low | .NET shops with existing artifact management |

### Rule resolution order

```
1. builtin://     ← shipped with engine (base)
2. imports        ← from imports.yaml (org layer)
3. rules.yaml     ← local additions (team layer)
4. overrides.yaml ← explicit overrides (must be allowed)
```

### Override control

Org-level packs can lock rules — teams can extend but not weaken:

```yaml
# In org pack
rules:
  - id: ARCH-001
    locked: true           # teams cannot override
    description: "Domain must not depend on Infrastructure"

  - id: NAMING-001
    locked: false           # teams can customize
    description: "Namespace convention"
```

---

## 4. MCP Tool Interface

### API endpoints (server mode)

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `GET /rules` | GET | Returns active rules (merged from all layers) |
| `GET /layers` | GET | Returns layer definitions and dependency graph |
| `GET /component-roles` | GET | Returns all valid roles for the active pattern |
| `GET /templates` | GET | Lists available built-in packs |
| `POST /init` | POST | Initialize project with a template |
| `POST /generate` | POST | Generate components (role + name in, code out) |
| `POST /generate-workspace` | POST | Scaffold solution/project structure |
| `POST /validate` | POST | Validate source files against active rules |
| `POST /lint` | POST | Naming, namespace, file path compliance only |

### CLI (local mode)

```bash
arch rules                          # show active rules
arch roles                          # list component roles
arch init --template hexagonal      # initialize project
arch generate --role Entity --name Order
arch validate ./src
arch lint ./src
arch sync                           # pull latest from imports
```

### Dual mode

- **Server mode** — ASP.NET API for MCP/AI agent consumption
- **CLI mode** — runs locally against `.architecture/` folder for developers and CI/CD

Same rule engine underneath, two interfaces.

---

## 5. Engine Architecture

```
ArchitectureGovernance/
├── src/
│   ├── Core/                          # Domain + rule engine
│   │   ├── RuleSchema/                # Models for layers, roles, rules
│   │   ├── RuleEngine/                # Merge, resolve, validate logic
│   │   ├── RuleLoader/                # Read YAML, resolve extends/imports
│   │   └── CodeGeneration/            # Pattern-agnostic scaffolding (Roslyn)
│   │
│   ├── BuiltinPacks/                  # Shipped templates (10 packs)
│   │   ├── clean-architecture.yaml
│   │   ├── hexagonal-architecture.yaml
│   │   ├── cqrs.yaml
│   │   ├── ddd-tactical.yaml
│   │   ├── event-driven.yaml
│   │   ├── vertical-slice.yaml
│   │   ├── modular-monolith.yaml
│   │   ├── onion-architecture.yaml
│   │   ├── layered.yaml
│   │   └── microservice.yaml
│   │
│   ├── Sync/                          # Import resolvers
│   │   ├── GitSyncProvider/           # Clone/pull from git repos
│   │   └── NuGetSyncProvider/         # Pull packs from NuGet feeds
│   │
│   ├── Host.Api/                      # ASP.NET MCP server
│   │   ├── Controllers/
│   │   └── Program.cs
│   │
│   └── Host.Cli/                      # CLI tool
│       └── Commands/                  # init, generate, validate, sync
│
├── tests/
├── docs/
└── .architecture/                     # Dogfooding — governs itself
```

### Key design decisions

- **Core has zero dependency on Host** — rule engine is a library, consumable by API, CLI, or embedded in CI
- **YAML is the source of truth** — no database for rules locally; the "database" is the git repo or NuGet feed
- **Roslyn for code generation** — proven approach from AIResearch.Architecture
- **Dogfooding** — the engine uses its own `.architecture/` folder

---

## 6. MVP Scope

| Priority | Feature |
|----------|---------|
| v1 | Rule schema + YAML loader | Done |
| v1 | 3 built-in packs: clean-architecture, hexagonal, cqrs | Done |
| v2 | 3 additional packs: onion-architecture, layered, microservice | Done |
| v1 | CLI: `init`, `generate`, `validate` | Done |
| v1 | API host (MCP compatible) | Done |
| v1 | Pack composability (`extends`) | Done |
| v1 | Source validation (Roslyn-based dependency checking) | Done |
| v1 | Workspace scaffolding (`generate-workspace`) | Done |
| v2 | Override control (locked rules) | Done |
| v2 | `lint` command | Done |
| v2 | Additional built-in packs (ddd-tactical, event-driven, vertical-slice, modular-monolith) | Done |
| v2 | MCP server (stdio + HTTP) with 7 tools + resources | Done |
| v3 | Git-based sync | Done |
| v3 | NuGet sync provider | — |
| v3 | CI/CD integration examples | — |

---

## 7. Verification

- **Unit tests:** Rule loading, merging, conflict detection, validation logic, workspace scaffolding (218 Core tests)
- **Integration tests:** End-to-end generate flow with all 10 built-in packs, scaffold→generate→validate pipeline
- **API tests:** Controller endpoints for generate, validate, lint, workspace scaffolding (12 tests)
- **MCP tests:** Tool-level tests for generate, validate, lint operations (22 tests)
- **Total:** 252 tests, all passing
- **Dogfooding:** The engine governs its own source structure
