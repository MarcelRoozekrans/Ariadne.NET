# MCP Server Integration — Design Document

**Date:** 2026-03-10
**Status:** Implemented

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
  - ModelContextProtocol 1.1.0
  - Microsoft.Extensions.Hosting 10.0.3

ArchGov.Host.Api.csproj (updated):
  + ArchGov.Host.Mcp (project ref — reuses tool/resource classes)
  + ModelContextProtocol.AspNetCore 1.1.0
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

| Resource URI | Description | Status |
|-------------|-------------|--------|
| `archgov://packs/{name}` | Full YAML content of a specific built-in pack | Implemented |
| `archgov://packs` | List all available pack names | Future |
| `archgov://active-rules` | The resolved rule pack for the current working directory | Future |

## Context Auto-Detection

The stdio server inherits the working directory from the AI agent that launches it. On each tool call:

1. Check if `.architecture/rules.yaml` exists in working directory
2. If yes, load and resolve it (including `extends` references to built-in packs)
3. Cache the resolved pack for the session (invalidate on file change if feasible)

This means AI agents working in a project with `.architecture/rules.yaml` get correct rules automatically — zero configuration.

## Stdio Server (`ArchGov.Host.Mcp`)

Console app with stdio transport:

```csharp
var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddArchGovCore();
builder.Services.AddSingleton<RuleResolutionHelper>();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

await builder.Build().RunAsync().ConfigureAwait(false);
```

Tools implemented as static classes with `[McpServerToolType]` and `[McpServerTool]` attributes. DI services injected as method parameters automatically by the SDK.

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

MCP HTTP transport added alongside existing REST endpoints:

```csharp
builder.Services.AddSingleton<RuleResolutionHelper>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(TemplateTools).Assembly)
    .WithResourcesFromAssembly(typeof(PackResources).Assembly);

// ...
app.MapMcp();
```

Same tools and resources from `ArchGov.Host.Mcp` assembly, different transport. Enables remote teams to share a single governance server.

## Tool Implementation Pattern

Each tool is a thin static wrapper that delegates to core services. DI services are injected as method parameters by the MCP SDK:

```csharp
[McpServerToolType]
public static class GenerateTools
{
    [McpServerTool(Name = "generate_component")]
    [Description("Generate a C# component that conforms to architecture rules.")]
    public static GenerateResult GenerateComponent(
        RuleResolutionHelper helper,
        IGenerateOrchestrator orchestrator,
        string role, string name, string solution, string feature,
        string? template = null)
    {
        var resolved = helper.Resolve(template);
        var request = new GenerateRequest { ... };
        return orchestrator.Generate(resolved, request);
    }
}
```

## Testing Strategy

- **Unit tests**: 10 tests covering all tool and resource classes with real core services (no mocks needed — core is stateless)
- **Integration tests**: covered by existing API integration tests (6 tests) and core integration tests (32 per-pack tests)

## Implemented

- 7 MCP tools (list_templates, get_layers, get_component_roles, get_rules, generate_component, validate_source, lint_source)
- 1 MCP resource template (archgov://packs/{name})
- Stdio transport (ArchGov.Host.Mcp)
- HTTP transport (ArchGov.Host.Api at /mcp)
- RuleResolutionHelper with 3-strategy resolution (explicit > .architecture/rules.yaml > clean-architecture)
- 10 unit tests

## Out of Scope (for now)

- MCP prompts (prompt templates for AI agents)
- MCP sampling (requesting AI completions from within the server)
- File watching / live reload of `.architecture/rules.yaml`
- Authentication on HTTP endpoint
- `archgov://packs` list resource
- `archgov://active-rules` resource
