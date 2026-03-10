# MCP Server Integration — Design Document

**Date:** 2026-03-10
**Status:** Approved

## Goal

Expose the Architecture Governance Engine as an MCP (Model Context Protocol) server so AI agents (Claude Code, Cursor, Copilot, etc.) can call it directly during code generation — enforcing architecture rules in real-time.

## Core Principle

AI generates behavior. The engine governs structure. MCP makes the governance invisible to users — AI agents call the engine automatically.

## Architecture

### Dual Transport

Two hosting modes, both using the official `ModelContextProtocol` .NET SDK:

1. **Stdio server** (`ArchGov.Host.Mcp`) — standalone console app launched as subprocess by AI agents. For local development.
2. **SSE/HTTP transport** (added to `ArchGov.Host.Api`) — remote MCP endpoint alongside existing REST routes. For team/CI use.

Both share the same `AddArchGovCore()` DI registration — identical engine underneath.

```
src/
  ArchGov.Core/                    # Unchanged — rule engine library
  ArchGov.Host.Api/                # UPDATED — add MCP SSE transport
  ArchGov.Host.Cli/                # Unchanged
  ArchGov.Host.Mcp/                # NEW — stdio MCP server
```

### NuGet Dependencies

```
ArchGov.Host.Mcp.csproj:
  - ArchGov.Core (project ref)
  - ModelContextProtocol (latest stable)

ArchGov.Host.Api.csproj (updated):
  + ModelContextProtocol.AspNetCore (latest stable)
```

## MCP Tools (7)

| Tool Name | Parameters | Returns | Description |
|-----------|-----------|---------|-------------|
| `list_templates` | _(none)_ | `string[]` names + descriptions | List available built-in templates |
| `get_layers` | `template?` | Layer definitions with allowed dependencies | Get architecture layers |
| `get_component_roles` | `template?` | Component role definitions | Get available component roles |
| `get_rules` | `template?` | Code feature rule definitions | Get code feature rules |
| `generate_component` | `role`, `name`, `solution`, `feature`, `template?` | File path + generated C# code | Generate a component |
| `validate_source` | `sourceDirectory`, `template?` | Validation result with violations | Validate source against rules |
| `lint_source` | `sourceDirectory`, `template?` | Lint result with violations | Lint for namespace-layer mismatches |

### Template Resolution

All `template?` parameters follow this resolution order:
1. If explicit `template` provided → use it
2. If `.architecture/rules.yaml` exists in working directory → load + resolve it
3. Fall back to `clean-architecture`

## MCP Resources

Rule packs exposed as readable resources so agents can inspect full definitions:

| Resource URI | Description |
|-------------|-------------|
| `archgov://packs` | List all available pack names |
| `archgov://packs/{name}` | Full YAML content of a specific built-in pack |
| `archgov://active-rules` | The resolved rule pack for the current working directory |

## Context Auto-Detection

The stdio server inherits the working directory from the AI agent that launches it. On each tool call:

1. Check if `.architecture/rules.yaml` exists in working directory
2. If yes, load and resolve it (including `extends` references to built-in packs)
3. Cache the resolved pack for the session (invalidate on file change if feasible)

This means AI agents working in a project with `.architecture/rules.yaml` get correct rules automatically — zero configuration.

## Stdio Server (`ArchGov.Host.Mcp`)

Minimal console app:

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddArchGovCore();
builder.Services.AddMcpServer()
    .WithStdioTransport()
    .WithTools()
    .WithResources();
var app = builder.Build();
await app.RunAsync();
```

Tools implemented as classes with `[McpServerTool]` and `[McpServerToolType]` attributes, or registered via the builder API.

### Agent Configuration

Claude Code (`.mcp.json`):
```json
{
  "mcpServers": {
    "archgov": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/ArchGov.Host.Mcp"]
    }
  }
}
```

## API MCP Transport (`ArchGov.Host.Api`)

Add SSE endpoint to existing API:

```csharp
app.MapMcp("/mcp");  // SSE transport at /mcp
```

Same tools and resources, different transport. Enables remote teams to share a single governance server.

## Tool Implementation Pattern

Each tool is a thin wrapper that delegates to core services:

```csharp
[McpServerToolType]
public class GenerateComponentTool
{
    [McpServerTool("generate_component"), Description("Generate a C# component...")]
    public GenerateResult Generate(
        IGenerateOrchestrator orchestrator,
        IRulePackResolver resolver,
        IBuiltinPackRegistry registry,
        string role, string name, string solution, string feature,
        string? template = null)
    {
        var resolved = ResolveRules(template, resolver, registry);
        var request = new GenerateRequest { ... };
        return orchestrator.Generate(resolved, request);
    }
}
```

## Testing Strategy

- **Unit tests**: each MCP tool class tested in isolation with mocked core services
- **Integration tests**: start stdio server in-process, send JSON-RPC messages, verify responses
- **Smoke tests**: launch actual process, connect via `ModelContextProtocol` client SDK

## Out of Scope (for now)

- MCP prompts (prompt templates for AI agents)
- MCP sampling (requesting AI completions from within the server)
- File watching / live reload of `.architecture/rules.yaml`
- Authentication on SSE endpoint
