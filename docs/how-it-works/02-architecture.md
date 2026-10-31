# Architecture

## Solution Structure

```mermaid
graph TD
    subgraph src
        Abstractions["Ariadne.NET.Abstractions<br/><i>Rule-pack schema + contracts<br/>ZeroAlloc.Results + Validation only, Native AOT</i>"]
        subgraph Ariadne.NET["Ariadne.NET (Rule engine library)"]
            RuleLoader["RuleLoader<br/><i>YAML loading, registry, extends resolution</i>"]
            RuleEngine["RuleEngine<br/><i>Merger, validator, inference</i>"]
            CodeGeneration["CodeGeneration<br/><i>Roslyn C# generator</i>"]
            Orchestration["Orchestration<br/><i>Generate + workspace</i>"]
            Validation["Validation<br/><i>Source validation & linting</i>"]
            Sync["Sync<br/><i>Git sync, semver, caching</i>"]
            BuiltinPacks["BuiltinPacks<br/><i>10 embedded YAML templates</i>"]
        end
        HostApi["Ariadne.NET.Mcp.Http<br/><i>MCP over HTTP</i>"]
        HostCli["Ariadne.NET.Cli<br/><i>CLI (System.CommandLine 3.0)</i>"]
        HostMcp["Ariadne.NET.Mcp<br/><i>MCP stdio server</i>"]
    end

    subgraph tests
        CoreTests["Ariadne.NET.Tests<br/><i>Unit + integration tests</i>"]
        CliTests["Ariadne.NET.Cli.Tests<br/><i>CLI exit-code tests</i>"]
        McpTests["Ariadne.NET.Mcp.Tests<br/><i>MCP tool + resource tests</i>"]
        AotSmoke["Ariadne.NET.AotSmoke<br/><i>Native AOT publish check</i>"]
    end
```

## Core Pipeline

The engine processes a user request through a series of stages:

```mermaid
flowchart TD
    A["User Request<br/><i>role, name, solution, feature</i>"] --> B

    subgraph Core["Ariadne.NET Pipeline"]
        B["1. RulePackResolver<br/><i>Parse extends references<br/>(builtin://, import://)<br/>Load referenced packs</i>"]
        B --> C["2. RulePackMerger<br/><i>Combine layers (union deps)<br/>Roles (last wins)<br/>Rules (locked protection)</i>"]
        C --> D["3. LayerInferenceEngine<br/><i>Map role name → layer<br/>Primary + alternative names<br/>Ambiguity detection</i>"]
        D --> E["4. DependencyValidator<br/><i>Check layer dependency<br/>graph constraints</i>"]
        E --> F["5. CodeGeneratorService<br/><i>Roslyn SyntaxFactory →<br/>formatted C# output</i>"]
    end

    F --> G["Generated File<br/><i>path, namespace, type, code</i>"]
```

## Dependency Flow

All three host applications depend on `Ariadne.NET`, which depends on `Ariadne.NET.Abstractions`. Abstractions holds the rule-pack schema and the loader, resolver and registry contracts, and depends on nothing, so other tools can reference it without pulling in Roslyn or YamlDotNet. The only host-to-host dependency is `Ariadne.NET.Mcp.Http` → `Ariadne.NET.Mcp`, so the HTTP transport serves the same MCP tools and resources as stdio.

```mermaid
graph LR
    Api["Ariadne.NET.Mcp.Http"] --> Core["Ariadne.NET"]
    Api --> Mcp
    Cli["Ariadne.NET.Cli"] --> Core
    Mcp["Ariadne.NET.Mcp"] --> Core
    Core --> Abs["Ariadne.NET.Abstractions"]
```

Within `Ariadne.NET`, dependencies flow strictly top-down:

```mermaid
graph TD
    Orchestration --> CodeGeneration
    Orchestration --> Validation
    Orchestration --> RuleEngine
    CodeGeneration --> RuleEngine
    Validation --> RuleEngine
    RuleEngine --> RuleLoader
    RuleLoader --> Abstractions["Ariadne.NET.Abstractions<br/><i>(separate package — ZeroAlloc.Results + Validation only)</i>"]
    Sync["Sync<br/><i>(independent module)</i>"]
```

---

Previous: [Overview](01-overview.md) | Next: [Rule Schema](03-rule-schema.md)
