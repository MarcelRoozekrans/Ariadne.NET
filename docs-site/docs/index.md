---
sidebar_position: 1
slug: /
title: Introduction
---

# Ariadne.NET

A pluggable, pattern-agnostic architecture governance engine that enforces structural rules for AI code generation via declarative YAML rule packs.

> **Core Principle:** AI generates behavior. The engine governs structure.

## Why

AI coding agents generate behavior effectively but are unreliable at structural decisions — they invent folder structures, improvise namespaces, and silently violate dependency rules. This engine separates structural governance from AI code generation: the AI declares intent, the engine enforces structure.

## Quick Start

To use the library from your own project, see [Getting started](getting-started.md).

### CLI

> Until the `ariadne` dotnet tool ships, run these commands as `dotnet run --project src/Ariadne.NET.Cli -- <command>`.

```bash
# Initialize a project with clean architecture rules
ariadne init --template clean-architecture

# Compose multiple patterns
ariadne init --template clean-architecture+cqrs

# List available component roles
ariadne roles

# Generate a component
ariadne generate --role Entity --name Order --solution MyApp --feature Orders
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

## MCP Tools

| Tool | Description |
|------|-------------|
| `list_templates` | List available architecture templates |
| `get_layers` | Get layers with their id, selectors, allowed dependencies and because |
| `get_component_roles` | Get available component roles |
| `get_rules` | Get the forbid and allowOnly rules |
| `generate_component` | Generate a C# component |
| `generate_workspace` | Scaffold project structure |
| `validate_source` | Validate a solution or project against rules |

## Tech Stack

- **.NET 10** — Runtime
- **YamlDotNet** — YAML parsing
- **Roslyn** — Code generation and semantic validation
- **System.CommandLine** — CLI framework
- **ModelContextProtocol** — MCP server SDK (stdio + HTTP)
- **ZeroAlloc.Inject** — Compile-time DI registration
- **xUnit + FluentAssertions** — Testing
