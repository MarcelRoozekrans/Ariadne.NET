# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A pattern-agnostic architecture governance engine for .NET. Architecture patterns (clean, hexagonal, CQRS, …) are declarative YAML **rule packs**; the engine resolves and merges them, then uses them to generate correctly placed C# components (Roslyn) and to validate/lint existing source. Core principle: *AI generates behavior, the engine governs structure.*

## Commands

The solution file is `src/ArchGov.slnx` (the .NET 10 XML format, not `.sln`).

```bash
dotnet build src/ArchGov.slnx
dotnet test src/ArchGov.slnx

# Single test project / single test
dotnet test tests/ArchGov.Core.Tests
dotnet test src/ArchGov.slnx --filter "FullyQualifiedName~RulePackMergerTests"
dotnet test src/ArchGov.slnx --filter "FullyQualifiedName=ArchGov.Core.Tests.RuleEngine.RulePackMergerTests.<Method>"

# Run hosts
dotnet run --project src/ArchGov.Host.Cli -- <init|rules|roles|generate|validate|lint|sync> [...]
dotnet run --project src/ArchGov.Host.Mcp      # MCP over stdio
dotnet run --project src/ArchGov.Host.Api      # MCP over HTTP at /mcp

# Docs site (Docusaurus, deployed to GitHub Pages)
cd docs-site && npm ci && npm run start
```

CI (`.github/workflows/ci.yml`) runs restore → build → test → pack in Release. There is no separate lint step: analyzers (Meziantou, Roslynator, ErrorProne.NET, NetFabric.Hyperlinq, ZeroAlloc) run during build via `Directory.Build.props`. Warnings are not errors and the build already carries some, so check that a change adds no new ones.

## Architecture

### Projects

- `ArchGov.Core` — the whole engine; the only packable project (published to NuGet). Must have no host dependencies.
- `ArchGov.Host.Cli` — System.CommandLine 3.0 (preview) CLI; one static `XxxCommand.Create(IServiceProvider)` per command in `Commands/`, wired in `CliRootCommand`. Commands return `ExitCodes` (0 success, 1 violations, 2 error); `CliRootCommand.Invoke` turns uncaught exceptions into 2. `tests/ArchGov.Host.Cli.Tests` changes the process working directory, so it runs without test parallelization.
- `ArchGov.Host.Mcp` — MCP stdio server. Tools are static methods on `[McpServerToolType]` classes in `Tools/`; resources in `Resources/` (`archgov://packs/{name}`). Services are injected as method parameters.
- `ArchGov.Host.Api` — **not a REST API anymore** (the REST controllers were removed). It is only the HTTP transport for the MCP tools, reusing the tool/resource assemblies from `ArchGov.Host.Mcp`.

### Pipeline

`RulePackResolver` → `RulePackMerger` → `LayerInferenceEngine` → `DependencyValidator` → `CodeGeneratorService`, wired by `GenerateOrchestrator` (components) and `WorkspaceOrchestrator` (solution scaffolding). `Validation/` (`SourceValidator`, `Linter`) parses existing source with Roslyn and checks `using` directives against layer dependencies and namespaces against directory layers.

- **Resolution:** a user pack (usually `.architecture/rules.yaml`) lists `extends:` references. `builtin://<name>` loads an embedded pack from `src/ArchGov.Core/BuiltinPacks/*.yaml` (registered via `EmbeddedResource`; adding a YAML file there adds a template). `import://<name>` loads from `.architecture/.cache/git/<name>/` and requires a working directory — populated by `arch sync` from `.architecture/imports.yaml` (`Sync/`: git CLI provider, semver tag ranges). Anything else throws `NotSupportedException`. The user pack is merged last.
- **Merge semantics** (`RulePackMerger`): layers merge by name (case-insensitive) with unioned `allowedDependencies`; component roles by name, later wins; code feature rules by id, but a `locked` rule cannot be overridden (emits a `MergeWarning`); naming conventions: last pack wins. Composite template expressions like `clean-architecture+cqrs` are parsed by `TemplateReference` into one `builtin://` reference per name; both `arch init` and the MCP `template` parameter use it.
- **Rule auto-detection in MCP:** `RuleResolutionHelper` uses an explicit `template` argument, else `.architecture/rules.yaml` in the current directory, else falls back to `clean-architecture`. The CLI instead requires `.architecture/rules.yaml` (created by `arch init`).

### Dependency injection

Registration is compile-time via **ZInject**: classes are annotated `[Singleton]` and the generator emits `services.AddArchGovCoreServices()` (Core) and `services.AddArchGovHostMcpServices()` (Mcp). A new service only needs the attribute; `ServiceRegistrationTests` asserts the Core services resolve.

### Repo's own rules

`.architecture/rules.yaml` governs this repo itself (Core / Host / Tests layers on top of `builtin://clean-architecture`): Core depends on nothing, hosts only orchestrate.

## Conventions enforced by analyzers

- Public APIs expose `IList<T>`/`IDictionary<,>`/`IReadOnlyList<T>`, not `List<T>`/`Dictionary<,>` (MA0016). Rule-schema models use `IList<T>`.
- String-keyed dictionaries and string comparisons need an explicit `StringComparer`/`StringComparison` (MA0002 etc.); the engine uses `OrdinalIgnoreCase` for names.
- One type per file (MA0048) — this is why records/enums each have their own file.
- `ConfigureAwait(false)` on awaits.
- Tests: xUnit + FluentAssertions (+ NSubstitute in Core.Tests). Test folders mirror the `src/ArchGov.Core` namespaces; MCP tool tests construct the real services directly rather than via DI.

## Docs

`docs/` (design/plan docs in `docs/plans/`, `docs/how-it-works/`) and `docs-site/docs/` are separate copies that have diverged; the Docusaurus site builds from `docs-site/docs/`. Update both when changing documented behavior.

## Releases

Conventional commits on `main` drive release-please (`release-type: simple`); `publish.yml` versions with GitVersion (Mainline mode) and pushes `ArchGov.Core` to NuGet and GitHub Packages on every push to `main`.
