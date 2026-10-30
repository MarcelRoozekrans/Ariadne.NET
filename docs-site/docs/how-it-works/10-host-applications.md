---
sidebar_position: 10
---

# Host Applications

The engine ships with three host applications, each targeting a different use case. All share the same core engine via `services.AddArchGovCoreServices()`.

## CLI (ArchGov.Host.Cli)

Built with **System.CommandLine 3.0** (uses `Command`, `RootCommand`, `Option<T>` — no `Cli` prefix).

**Commands:**

### `init` — Initialize architecture rules

```bash
arch init --template clean-architecture
arch init --template clean-architecture+cqrs+ddd-tactical
```

- Splits template string by `+`
- Validates each pack name exists in the registry
- Creates `.architecture/rules.yaml` with `extends` list

### `rules` — Print resolved layer rules

```bash
arch rules
```

- Loads `.architecture/rules.yaml` from current directory
- Resolves all extends references
- Prints layers with their allowed dependencies

### `roles` — Print component roles table

```bash
arch roles
```

- Resolves rules, then prints a formatted table:
  `Name | Layer | TypeKind | RequiresInterface | Description`

### `generate` — Generate a component

```bash
arch generate --role Entity --name Order --solution MyApp --feature Orders
```

| Option | Short | Type | Description |
|--------|-------|------|-------------|
| `--role` | `-r` | `string` | Component role (e.g., `Entity`, `Repository`) |
| `--name` | `-n` | `string` | Component name (e.g., `Order`) |
| `--solution` | `-s` | `string` | Solution name |
| `--feature` | `-f` | `string` | Feature name |

Prints the generated file path, namespace, type name, and full source code.

### `validate` — Validate source code

```bash
arch validate ./src
arch validate /path/to/source
```

- Argument: `path` (default: `"./src"`)
- Validates directory exists
- Runs full two-pass validation
- Prints files scanned and any violations

### `lint` — Lint source code

```bash
arch lint ./src
```

- Argument: `path` (default: `"./src"`)
- Runs namespace-vs-directory consistency check
- Prints files scanned and any violations

### `sync` — Sync rule packs from git

```bash
arch sync                              # sync all imports
arch sync --name company-standards     # sync one import
arch sync --no-cache                   # force fresh fetch
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

## MCP HTTP Host (ArchGov.Host.Api)

A thin ASP.NET Core host that exposes the MCP server over HTTP for remote/team access. No REST controllers — all capabilities are served via the MCP protocol at `/mcp`.

```csharp
builder.Services.AddArchGovCoreServices();
builder.Services.AddArchGovHostMcpServices();   // registers RuleResolutionHelper
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(TemplateTools).Assembly)
    .WithResourcesFromAssembly(typeof(PackResources).Assembly);

app.MapMcp();
```

The same MCP tools and resources available via stdio are available over HTTP — zero duplication between hosts.

## MCP Server (ArchGov.Host.Mcp)

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
| `validate_source` | `sourceDirectory`, `template?` | `SourceValidationResult` | Validate against rules |
| `lint_source` | `sourceDirectory`, `template?` | `LintResult` | Lint for mismatches |

**MCP Resources:**

| URI Template | Returns | Description |
|-------------|---------|-------------|
| `archgov://packs/{name}` | Raw YAML string | Full content of a built-in pack |

All tools auto-detect `.architecture/rules.yaml` in the working directory. The `template` parameter overrides auto-detection.

---

