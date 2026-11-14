---
sidebar_position: 3
---

# Features

Complete feature reference for Ariadne.NET.

## Declarative Rule Packs

Define architecture patterns as YAML — no code changes needed to support new patterns.

- **Layers** that cover types by namespace globs and assembly names, with explicit dependency rules (e.g., Domain depends on nothing, Application depends on Domain)
- **Rules** that `forbid` a dependency, `allowOnly` named users of one, or `require` selected types to live in a layer and have a shape, each with exceptions and a `because` that every violation repeats
- **Component roles** with type kind (class, interface, record, struct, record struct), accessibility, and interface requirements
- **Naming conventions** with template variables (`{SolutionName}`, `{Feature}`, `{Layer}`, `{ComponentName}`)
- **A strict loader**: unknown keys, missing keys and wrong types fail the load with `file:line:column`

```yaml
schema: 2
name: my-service
extends:
  - builtin://clean-architecture

layers:
  - name: Domain
    assemblies: [MyService.Domain]
  - name: Application
    assemblies: [MyService.Application]
  - name: Infrastructure
    assemblies: [MyService.Infrastructure]
  - name: WebApi
    assemblies: [MyService.Api]

rules:
  - id: MYSERVICE-NO-EFCORE-IN-DOMAIN
    because: Persistence belongs to Infrastructure.
    forbid:
      from: { layers: [Domain] }
      to: { namespaces: ["Microsoft.EntityFrameworkCore.**"] }

componentRoles:
  - name: Entity
    layer: Domain
    typeKind: Class
    accessibility: public

namingConventions:
  namespace: "{SolutionName}.{Feature}.{Layer}"
  filePath: "src/{SolutionName}.{Feature}.{Layer}/{ComponentName}.cs"
```

The four layer entries have only a name and selectors, so they retarget the layers of `clean-architecture` to this solution's assemblies. See [Rule Schema](how-it-works/03-rule-schema.md) for the full format.

## Built-in Templates

Ten templates ship out of the box, covering the most common architectural patterns:

| Template | Layers | Roles | Type |
|----------|--------|-------|------|
| `clean-architecture` | Domain, Application, Infrastructure, WebApi | 19 | Standalone |
| `onion-architecture` | DomainModel, DomainServices, ApplicationServices, Infrastructure, Presentation | 14 | Standalone |
| `hexagonal-architecture` | Core, Ports, Adapters | 7 | Standalone |
| `layered` | Presentation, Business, DataAccess | 11 | Standalone |
| `microservice` | Domain, Application, Infrastructure, Api, Contracts | 17 | Standalone |
| `event-driven` | Domain, Events, Producers, Consumers, Infrastructure | 9 | Standalone |
| `vertical-slice` | Feature, SharedKernel, Infrastructure | 8 | Standalone |
| `modular-monolith` | Module.Public, Module.Core, Module.Infrastructure, Host | 8 | Standalone |
| `cqrs` | _(composable)_ | 7 | Add-on |
| `ddd-tactical` | _(composable)_ | 9 | Add-on |

**Standalone** templates define their own layers and can be used independently. **Add-on** templates define only component roles and are designed to be composed with a standalone template: a role-only pack resolves on its own, but combining it with a base pack that declares the layers its roles name (for example Application and Domain) is recommended. When the merged pack declares at least one layer, a role naming an undeclared layer is an Invalid error. Two standalone templates do not compose, because both define layers; see [Composability](#composability--merge-semantics).

## Composability & Merge Semantics

Multiple rule packs can be composed via the `extends` field:

```yaml
extends:
  - builtin://clean-architecture
  - builtin://cqrs
  - builtin://ddd-tactical
```

Packs merge parent first, in `extends` order, then the child:

| Element | Merge Strategy |
|---------|---------------|
| Layers | Defined once. Two packs that define the same layer name fail the load. A child can retarget an inherited layer's selectors (`namespaces`, `assemblies`) and nothing else about it. |
| Rules | Defined once. Two packs that define the same rule id fail the load. A child can add rules and add `exceptions` to an inherited rule that is not `locked`. |
| Component Roles | Merged by name. Later pack overrides earlier. |
| Naming Conventions | Last pack wins. |

Validation rejects an empty `Namespace` or `FilePath` in the naming conventions.

A solution picks one base architecture and retargets it. `clean-architecture+cqrs` composes because `cqrs` defines roles only; `clean-architecture+microservice` fails because both define `Domain`, `Application` and `Infrastructure`.

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

## Semantic Validation

Checks a solution or project against the architecture rules with the compiler's semantic model. `Ariadne.NET.Workspaces` loads the projects with MSBuild, and the engine builds a type graph from each compilation. A dependency is a type reference somebody wrote: base types, interfaces, attributes, member signatures, generic arguments and types used in bodies. An unused `using` and `var` give nothing. Generated code is never a source, except the code of Razor components and views (`.razor`, `.cshtml`), which is checked like hand-written code and reported at the Razor file.

Detected violations:
- **DependencyNotAllowed** — a type references a type in a layer that is not in its layer's `allowedDependencies`. One violation per source type and target type, located at the first reference, with a reference count. Carries the layer's id and `because`.
- **ForbiddenDependency** — a type in a `forbid` rule's `from` references a type in its `to`, and no exception matches the source type.
- **DependencyNotPermitted** — a type references a type in an `allowOnly` rule's `to`, and is neither in `from`, nor in `to` itself, nor matched by an exception.
- **LayerOverlap** — a type is covered by more than one layer. It is reported once and left out of the dependency check.
- **WrongLayer** — a type selected by a `require` rule is not in the layer the rule requires
- **TypeKindMismatch** — a type doesn't match the role's or a `require` rule's expected type kind
- **AccessibilityMismatch** — a type's accessibility doesn't match the role definition or `require` rule; a top-level type with no access modifier counts as internal (a nested one is private)
- **MissingInterface** — a role requires an interface but none was found, or a type selected by a `require` rule implements no interface its `implements` selector matches

Each violation has a rule id: a layer id, a rule id, or `ROLE-<Name>`, `CODE-001` or `CODE-002` for component roles. Every violation also carries the `because` of its layer or rule. A project that targets several frameworks is checked once per framework, and a violation found in only some of them lists them. See [Validation](how-it-works/08-validation.md) for the details.

Ariadne never restores, so run `dotnet restore` before you validate. The CLI exits 0 when clean, 1 on violations and 2 on an error.

> These commands assume the installed `ariadne` tool; see [Install the tool](getting-started.md#install-the-tool). From a clone of the repository, run them as `dotnet run --project src/Ariadne.NET.Cli -- <command>`.

```bash
# CLI
ariadne validate MyApp.slnx
```

The path is a `.sln`, `.slnx`, `.slnf` or `.csproj` file, or a directory that holds exactly one; it defaults to `.`. The package that does the loading, `Ariadne.NET.Workspaces`, needs a .NET SDK on the machine.

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

## Agent Hooks

`ariadne hook <event>` gives a Claude Code agent the structural rules and feeds violations back to it. `ariadne init --agent claude` installs the hooks in `.claude/settings.json`. See [Host Applications](how-it-works/10-host-applications.md) for the details.

| Event | What it does |
|-------|--------------|
| `session-start` | Adds the resolved layers, the rules with their `because`, the component roles, the naming conventions and the cached pack versions to the session. It warns when the cache is missing or stale. The text is capped at 10,000 characters and ends in a `Truncated` line when it was cut, dropping roles, rules, layers, the naming line and warnings in that order. |
| `pre-tool-use` | Asks the user, and never denies, before the agent writes a new `.cs` file whose assembly and namespace no layer selects. It also records the edited `.cs` files for `stop`. |
| `stop` | Once per turn, after `.cs` edits, validates the solution and feeds at most 20 violations back, each with its message and, when it has one, its rule id and `because`; the text is capped at 10,000 characters. Silent when the solution is clean. |

The hooks fail open: an error is one line on stderr, stdout stays empty and the exit code is 0. The stale-cache check reads only `.architecture/imports.yaml` and the cache folder and never uses the network. The `ariadne` command must be on `PATH`; [install the tool](getting-started.md#install-the-tool) with `dotnet tool install --global Ariadne.NET.Cli`, or write the settings entries by hand as shown in [Host Applications](how-it-works/10-host-applications.md).

Ariadne's hooks run beside AI.Sentinel's hook. Ariadne owns the structure rules and their delivery; AI.Sentinel owns tool-call enforcement.

## MCP Server Integration

The engine exposes its full capability set as MCP tools and resources for AI agent integration.

### Tools

| Tool | Parameters | Description |
|------|-----------|-------------|
| `list_templates` | — | List available architecture templates |
| `get_layers` | `template?` | Get layers with id, selectors, dependency rules and because |
| `get_component_roles` | `template?` | Get available component roles |
| `get_rules` | `template?` | Get the forbid and allowOnly rules |
| `generate_component` | `role`, `name`, `solution`, `feature`, `template?` | Generate a C# component file |
| `generate_workspace` | `solution`, `features`, `template?` | Scaffold project structure |
| `validate_source` | `path`, `template?` | Validate a solution or project against architecture rules; returns the report |

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

Create your own YAML rule packs with custom layers, roles, and rules. Extend a built-in template or start from scratch:

```yaml
schema: 2
name: my-custom-rules
extends:
  - builtin://clean-architecture

rules:
  - id: MYORG-NO-DBCONTEXT-IN-DOMAIN
    because: The domain must not use DbContext directly.
    locked: true
    forbid:
      from: { layers: [Domain] }
      to: { types: ["**.DbContext"] }

componentRoles:
  - name: OutboxMessage
    layer: Infrastructure
    typeKind: Class
    accessibility: public
```

### Locked rules

Mark a rule `locked: true` to keep downstream teams from loosening it. A pack that extends yours can add rules, but it cannot add exceptions to a locked rule: the load fails and names the rule and both packs. Rules that are not locked accept exceptions from a child pack, each with its own `because`.
