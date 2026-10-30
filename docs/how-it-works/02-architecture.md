# Architecture

## Solution Structure

```mermaid
graph TD
    subgraph src
        subgraph ArchGov.Core["ArchGov.Core (Rule engine library)"]
            RuleSchema["RuleSchema<br/><i>Domain models</i>"]
            RuleLoader["RuleLoader<br/><i>YAML parsing, registry, resolver</i>"]
            RuleEngine["RuleEngine<br/><i>Merger, validator, inference</i>"]
            CodeGeneration["CodeGeneration<br/><i>Roslyn C# generator</i>"]
            Orchestration["Orchestration<br/><i>Generate + workspace</i>"]
            Validation["Validation<br/><i>Source validation & linting</i>"]
            Sync["Sync<br/><i>Git sync, semver, caching</i>"]
            BuiltinPacks["BuiltinPacks<br/><i>10 embedded YAML templates</i>"]
        end
        HostApi["ArchGov.Host.Api<br/><i>MCP over HTTP</i>"]
        HostCli["ArchGov.Host.Cli<br/><i>CLI (System.CommandLine 3.0)</i>"]
        HostMcp["ArchGov.Host.Mcp<br/><i>MCP stdio server</i>"]
    end

    subgraph tests
        CoreTests["ArchGov.Core.Tests<br/><i>Unit + integration tests</i>"]
        McpTests["ArchGov.Host.Mcp.Tests<br/><i>MCP tool + resource tests</i>"]
    end
```

## Core Pipeline

The engine processes a user request through a series of stages:

```mermaid
flowchart TD
    A["User Request<br/><i>role, name, solution, feature</i>"] --> B

    subgraph Core["ArchGov.Core Pipeline"]
        B["1. RulePackResolver<br/><i>Parse extends references<br/>(builtin://, import://)<br/>Load referenced packs</i>"]
        B --> C["2. RulePackMerger<br/><i>Combine layers (union deps)<br/>Roles (last wins)<br/>Rules (locked protection)</i>"]
        C --> D["3. LayerInferenceEngine<br/><i>Map role name → layer<br/>Primary + alternative names<br/>Ambiguity detection</i>"]
        D --> E["4. DependencyValidator<br/><i>Check layer dependency<br/>graph constraints</i>"]
        E --> F["5. CodeGeneratorService<br/><i>Roslyn SyntaxFactory →<br/>formatted C# output</i>"]
    end

    F --> G["Generated File<br/><i>path, namespace, type, code</i>"]
```

## Dependency Flow

All three host applications depend on `ArchGov.Core`. The only host-to-host dependency is `ArchGov.Host.Api` → `ArchGov.Host.Mcp`, so the HTTP transport serves the same MCP tools and resources as stdio.

```mermaid
graph LR
    Api["ArchGov.Host.Api"] --> Core["ArchGov.Core"]
    Api --> Mcp
    Cli["ArchGov.Host.Cli"] --> Core
    Mcp["ArchGov.Host.Mcp"] --> Core
```

Within `ArchGov.Core`, dependencies flow strictly top-down:

```mermaid
graph TD
    Orchestration --> CodeGeneration
    Orchestration --> Validation
    Orchestration --> RuleEngine
    CodeGeneration --> RuleEngine
    Validation --> RuleEngine
    RuleEngine --> RuleLoader
    RuleLoader --> RuleSchema["RuleSchema<br/><i>(leaf — no dependencies)</i>"]
    Sync["Sync<br/><i>(independent module)</i>"]
```

---

Previous: [Overview](01-overview.md) | Next: [Rule Schema](03-rule-schema.md)
