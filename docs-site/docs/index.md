---
sidebar_position: 1
slug: /
title: Introduction
---

# Architecture Governance Engine

A pluggable, pattern-agnostic architecture governance engine that enforces structural rules for AI code generation via declarative YAML rule packs.

> **Core Principle:** AI generates behavior. The engine governs structure.

## Why

AI coding agents generate behavior effectively but are unreliable at structural decisions — they invent folder structures, improvise namespaces, and silently violate dependency rules. This engine separates structural governance from AI code generation: the AI declares intent, the engine enforces structure.

## Quick Start

### CLI

```bash
# Initialize a project with clean architecture rules
arch init --template clean-architecture

# Compose multiple patterns
arch init --template clean-architecture+cqrs

# List available component roles
arch roles

# Generate a component
arch generate --role Entity --name Order --solution MyApp --feature Orders
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

## MCP Tools

| Tool | Description |
|------|-------------|
| `list_templates` | List available architecture templates |
| `get_layers` | Get layer definitions with allowed dependencies |
| `get_component_roles` | Get available component roles |
| `get_rules` | Get code feature rules |
| `generate_component` | Generate a C# component |
| `generate_workspace` | Scaffold project structure |
| `validate_source` | Validate source files against rules |
| `lint_source` | Lint for namespace-layer mismatches |

## Tech Stack

- **.NET 10** — Runtime
- **YamlDotNet** — YAML parsing
- **Roslyn** — Code generation and source validation
- **System.CommandLine** — CLI framework
- **ModelContextProtocol** — MCP server SDK (stdio + HTTP)
- **ZInject** — Compile-time DI registration
- **xUnit + FluentAssertions** — Testing
