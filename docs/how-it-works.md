# How It Works

Extensive technical documentation for the Architecture Governance Engine — a pluggable, pattern-agnostic governance system that enforces structural rules for AI code generation via declarative YAML rule packs.

> **Core Principle:** AI generates behavior. The engine governs structure.

## Documentation

| # | Section | Description |
|---|---------|-------------|
| 1 | [Overview](how-it-works/01-overview.md) | What the engine does and doesn't do |
| 2 | [Architecture](how-it-works/02-architecture.md) | Solution structure, core pipeline, dependency flow |
| 3 | [Rule Schema](how-it-works/03-rule-schema.md) | Domain models: RulePack, layers, roles, rules, naming conventions |
| 4 | [Rule Loading Pipeline](how-it-works/04-rule-loading.md) | YAML deserialization, builtin registry, pack resolver |
| 5 | [Rule Engine](how-it-works/05-rule-engine.md) | Merge algorithm, dependency validation, layer inference |
| 6 | [Code Generation](how-it-works/06-code-generation.md) | Roslyn pipeline, type creation, accessibility, formatting |
| 7 | [Orchestration](how-it-works/07-orchestration.md) | Generate and workspace orchestrators |
| 8 | [Validation and Linting](how-it-works/08-validation.md) | Two-pass validation algorithm, linting, violation types |
| 9 | [Git Sync](how-it-works/09-git-sync.md) | Imports manifest, semver resolution, caching, git workflow |
| 10 | [Host Applications](how-it-works/10-host-applications.md) | CLI commands, REST API endpoints, MCP tools |
| 11 | [Dependency Injection](how-it-works/11-dependency-injection.md) | Service registration table and lifetime rationale |
| 12 | [Built-in Templates](how-it-works/12-builtin-templates.md) | All 10 templates with layer diagrams and role tables |

## Tech Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| Runtime | .NET 10 | Target framework |
| YAML | YamlDotNet | Rule pack deserialization |
| Code Generation | Microsoft.CodeAnalysis.CSharp (Roslyn) | C# syntax tree construction and formatting |
| CLI | System.CommandLine 3.0 | Command-line interface |
| API | ASP.NET Core + Asp.Versioning | REST API with versioned endpoints |
| MCP | ModelContextProtocol SDK | AI agent integration (stdio + HTTP) |
| Testing | xUnit + FluentAssertions | Unit and integration tests |
| Analyzers | Meziantou, Roslynator, ErrorProne.NET, NetFabric.Hyperlinq, ZeroAlloc | Compile-time code quality enforcement |
