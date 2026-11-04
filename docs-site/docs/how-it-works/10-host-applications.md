---
sidebar_position: 10
---

# Host Applications

The engine ships with three host applications, each targeting a different use case. All share the same core engine via `services.AddAriadneNETServices()`.

## CLI (Ariadne.NET.Cli)

Built with **System.CommandLine 3.0** (uses `Command`, `RootCommand`, `Option<T>` — no `Cli` prefix).

**Commands:**

### `init` — Initialize architecture rules

> Until the `ariadne` dotnet tool ships, run these commands as `dotnet run --project src/Ariadne.NET.Cli -- <command>`.

```bash
ariadne init --template clean-architecture
ariadne init --template clean-architecture+cqrs+ddd-tactical
```

- Splits template string by `+`
- Validates each pack name exists in the registry
- Creates `.architecture/rules.yaml` with `extends` list

### `rules` — Print resolved layer rules

```bash
ariadne rules
```

- Loads `.architecture/rules.yaml` from current directory
- Resolves all extends references
- Prints layers with their allowed dependencies

### `roles` — Print component roles table

```bash
ariadne roles
```

- Resolves rules, then prints a formatted table:
  `Name | Layer | TypeKind | RequiresInterface | Description`

### `generate` — Generate a component

```bash
ariadne generate --role Entity --name Order --solution MyApp --feature Orders
```

| Option | Short | Type | Description |
|--------|-------|------|-------------|
| `--role` | `-r` | `string` | Component role (e.g., `Entity`, `Repository`) |
| `--name` | `-n` | `string` | Component name (e.g., `Order`) |
| `--solution` | `-s` | `string` | Solution name |
| `--feature` | `-f` | `string` | Feature name |

Prints the generated file path, namespace, type name, and full source code.

### `validate` — Validate a solution or project

```bash
ariadne validate
ariadne validate MyApp.slnx
ariadne validate /path/to/MyApp.csproj
```

- Argument: `path` (default: `"."`), a `.sln`, `.slnx`, `.slnf` or `.csproj` file, or a directory that holds exactly one of them
- Loads the projects with `ISolutionValidator` and checks each one against the rules; Ariadne never restores, so run `dotnet restore` first
- Prints the number of types and projects checked, a `Skipped` line for each skipped project, and each violation as `[Kind] file:line` followed by its message; a violation found in only some target frameworks lists them after the location, as in `[Kind] file:line (net10.0)`
- Exit codes: 0 no violations, 1 violations, 2 error. A cancelled run prints `Cancelled.`

```
Checked 2 types in 2 projects
Result: FAIL - 1 violation(s) found.

  [DependencyNotAllowed] Shop.Domain/Order.cs:7
    Layer 'Domain' is not allowed to depend on 'Infrastructure': Shop.Domain.Order references Shop.Infrastructure.SqlStore 3 times
```

File paths are shown relative to the directory of the solution or project.

### `lint` — Lint source code

```bash
ariadne lint ./src
```

- Argument: `path` (default: `"./src"`)
- Runs namespace-vs-directory consistency check
- Prints files scanned and any violations

### `sync` — Sync rule packs from git

```bash
ariadne sync                              # sync all imports
ariadne sync --name company-standards     # sync one import
ariadne sync --no-cache                   # force fresh fetch
```

| Option | Short | Type | Description |
|--------|-------|------|-------------|
| `--no-cache` | — | `bool` | Force fresh sync, ignore cache |
| `--name` | `-n` | `string?` | Sync only this import |

- Loads `.architecture/imports.yaml`
- Cache directory: `.architecture/.cache/git/`
- Prints import name, resolved version, and cached/fetched status

### Rules file resolution

All commands that need rules use `RulesCommand.ResolveRulesFromFile()`:

1. Look for `.architecture/rules.yaml` in the current directory
2. If not found, throw `FileNotFoundException` with helpful message
3. Load with `IRulePackLoader`
4. Resolve with `IRulePackResolver`
5. Return resolved `RulePack`

## MCP HTTP Host (Ariadne.NET.Mcp.Http)

A thin ASP.NET Core host that exposes the MCP server over HTTP for remote/team access. No REST controllers — all capabilities are served via the MCP protocol at `/mcp`.

```csharp
builder.Services.AddAriadneNETServices();
builder.Services.AddAriadneNETMcpServices();   // registers RuleResolutionHelper
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(TemplateTools).Assembly)
    .WithResourcesFromAssembly(typeof(PackResources).Assembly);

app.MapMcp();
```

The same MCP tools and resources available via stdio are available over HTTP — zero duplication between hosts.

## MCP Server (Ariadne.NET.Mcp)

Built with the **ModelContextProtocol SDK** for AI agent integration.

**Transports:**
- **Stdio** — local process communication (for Claude Code, Cursor, etc.)
- **HTTP** — remote access via the API server's `/mcp` endpoint

**RuleResolutionHelper** — shared logic for all MCP tools:

```mermaid
flowchart TD
    A["Resolve(template?)"] --> B{"template<br/>provided?"}
    B -->|Yes| C["Split template on '+'<br/>extend builtin:// per name<br/>→ Resolve"]
    B -->|No| D{".architecture/rules.yaml<br/>exists?"}
    D -->|Yes| E["Load and resolve<br/>rules.yaml"]
    D -->|No| F["Default to<br/>builtin://clean-architecture"]
```

**MCP Tools:**

| Tool | Parameters | Returns | Description |
|------|-----------|---------|-------------|
| `list_templates` | — | `IReadOnlyList<string>` | List available templates |
| `get_layers` | `template?` | `IList<LayerDefinition>` | Layer definitions with dependencies |
| `get_component_roles` | `template?` | `IList<ComponentRoleDefinition>` | Available roles for code generation |
| `get_rules` | `template?` | `IList<CodeFeatureRuleDefinition>` | Structural enforcement rules |
| `generate_component` | `role`, `name`, `solution`, `feature`, `template?` | `GenerateResult` | Generate a C# component |
| `generate_workspace` | `solution`, `features`, `template?` | `WorkspaceGenerateResult` | Scaffold project structure |
| `validate_source` | `path`, `template?` | `ArchitectureReport` | Validate a solution or project against rules |
| `lint_source` | `sourceDirectory`, `template?` | `LintResult` | Lint for mismatches |

**MCP Resources:**

| URI Template | Returns | Description |
|-------------|---------|-------------|
| `ariadne://packs/{name}` | Raw YAML string | Full content of a built-in pack |

All tools auto-detect `.architecture/rules.yaml` in the working directory. The `template` parameter overrides auto-detection.

---

