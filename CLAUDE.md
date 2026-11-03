# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A pattern-agnostic architecture governance engine for .NET. Architecture patterns (clean, hexagonal, CQRS, …) are declarative YAML **rule packs**; the engine resolves and merges them, then uses them to generate correctly placed C# components (Roslyn) and to validate/lint existing source. Core principle: *AI generates behavior, the engine governs structure.*

## Commands

The solution file is `src/Ariadne.NET.slnx` (the .NET 10 XML format, not `.sln`).

```bash
dotnet build src/Ariadne.NET.slnx
dotnet test src/Ariadne.NET.slnx

# Single test project / single test
dotnet test tests/Ariadne.NET.Tests
dotnet test src/Ariadne.NET.slnx --filter "FullyQualifiedName~RulePackMergerTests"
dotnet test src/Ariadne.NET.slnx --filter "FullyQualifiedName=Ariadne.NET.Tests.RuleEngine.RulePackMergerTests.<Method>"

# Run hosts
dotnet run --project src/Ariadne.NET.Cli -- <init|rules|roles|generate|validate|lint|sync> [...]
dotnet run --project src/Ariadne.NET.Mcp        # MCP over stdio
dotnet run --project src/Ariadne.NET.Mcp.Http   # MCP over HTTP at /mcp

# Native AOT check for Ariadne.NET.Abstractions: fails on any trim or AOT warning
dotnet publish tests/Ariadne.NET.AotSmoke -c Release -r linux-x64 -p:PublishAot=true -warnaserror

# Docs site (Docusaurus, deployed to GitHub Pages)
cd docs-site && npm ci && npm run start
```

CI (`.github/workflows/ci.yml`) builds and tests on ubuntu and windows, with a coverage gate and a codecov upload on ubuntu. It also runs `aot-smoke` and `pack-validate`, `commitlint` on pull requests, and `publish-nuget` on a tag dispatch. `.github/workflows/baseline-checks.yml` runs the ci-baseline v1 checks: gitleaks, CodeQL and NuGetAudit. There is no separate lint step: analyzers (Meziantou, Roslynator, ErrorProne.NET, NetFabric.Hyperlinq, ZeroAlloc) run during build via `Directory.Build.props`. `TreatWarningsAsErrors` is on in `Directory.Build.props`, so the build must stay at zero warnings. ZeroAlloc.Analyzers is at 1.12.0.

## Architecture

### Projects

- `Ariadne.NET.Abstractions` — the rule-pack schema (`RulePack`, `LayerDefinition`, `ComponentRoleDefinition`, …) and the pack contracts `IRulePackLoader`, `IRulePackResolver`, `IBuiltinPackRegistry`, all in the one flat namespace `Ariadne.NET.Abstractions`. Packable. Its only dependencies are `ZeroAlloc.Results` 1.4.5 and `ZeroAlloc.Validation` 2.5.0 (which bundles its generator; there is no separate Generator reference), and it is `IsAotCompatible`; `tests/Ariadne.NET.AotSmoke` proves it publishes with Native AOT. It also holds `AriadneError`. Keep it that way: no ZeroAlloc.Inject, no YamlDotNet, no loading logic. The validation contracts stay in the engine until M2 settles their shape.
- `Ariadne.NET` — the engine: YAML loading, `extends` resolution, merging, built-in packs, validation, sync and code generation. Packable; depends on Abstractions. Must have no host dependencies.
- `Ariadne.NET.Cli` — System.CommandLine 3.0 (preview) CLI; one static `XxxCommand.Create(IServiceProvider)` per command in `Commands/`, wired in `CliRootCommand`. Commands return `ExitCodes` (0 success, 1 violations, 2 error); `CliRootCommand.Invoke` turns uncaught exceptions into 2. `tests/Ariadne.NET.Cli.Tests` changes the process working directory, so it runs without test parallelization.
- `Ariadne.NET.Mcp` — MCP stdio server. Tools are static methods on `[McpServerToolType]` classes in `Tools/`; resources in `Resources/` (`ariadne://packs/{name}`). Services are injected as method parameters.
- `Ariadne.NET.Mcp.Http` — **not a REST API anymore** (the REST controllers were removed). It is only the HTTP transport for the MCP tools, reusing the tool/resource assemblies from `Ariadne.NET.Mcp`.

### Pipeline

`RulePackResolver` → `RulePackMerger` → `LayerInferenceEngine` → `DependencyValidator` → `CodeGeneratorService`, wired by `GenerateOrchestrator` (components) and `WorkspaceOrchestrator` (solution scaffolding). `Validation/` (`SourceValidator`, `Linter`) parses existing source with Roslyn and checks `using` directives against layer dependencies and namespaces against directory layers.

- **Resolution:** a user pack (usually `.architecture/rules.yaml`) lists `extends:` references. `builtin://<name>` loads an embedded pack from `src/Ariadne.NET/BuiltinPacks/*.yaml` (registered via `EmbeddedResource`; adding a YAML file there adds a template). `import://<name>` loads from `.architecture/.cache/git/<name>/` and requires a working directory — populated by `ariadne sync` from `.architecture/imports.yaml` (`Sync/`: git CLI provider, semver tag ranges). Anything else is an `Invalid` Result. The user pack is merged last.
- **Validation:** at load, each pack's member rules run through the five `*Validator` types that ZeroAlloc.Validation generates in Abstractions: non-empty names, layers and ids, a defined `typeKind`, no null list entries. After every resolve, `RulePackReferenceCheck` checks cross-references on the merged pack: duplicate layer or role names, unknown layers in `allowedDependencies`, a role's unknown layer, an unknown `interfaceRole`. Both report `Rule pack '{name}' is invalid.` with one `path: reason` line per failure as Detail, built by `RulePackError`. A role-only pack (`cqrs`, `ddd-tactical`) has no layers and resolves on its own, but should be combined with a base pack that declares the layers its roles name; when the merged pack declares at least one layer, a role naming an undeclared layer is an `Invalid` error.
- **Merge semantics** (`RulePackMerger`): layers merge by name (case-insensitive) with unioned `allowedDependencies`; component roles by name, later wins; code feature rules by id, but a `locked` rule cannot be overridden (emits a `MergeWarning`); naming conventions: last pack wins. Composite template expressions like `clean-architecture+cqrs` are parsed by `TemplateReference` into one `builtin://` reference per name; both `ariadne init` and the MCP `template` parameter use it.
- **Rule auto-detection in MCP:** `RuleResolutionHelper` uses an explicit `template` argument, else `.architecture/rules.yaml` in the current directory, else falls back to `clean-architecture`. The CLI instead requires `.architecture/rules.yaml` (created by `ariadne init`).

### Dependency injection

Registration is compile-time via **ZeroAlloc.Inject** 1.11.6: classes are annotated `[Singleton]` and the generator emits the internal `AddAriadneNETGeneratedServices()` (Core). The hand-written public `AddAriadneNETServices()` (`AriadneNETServiceCollectionExtensions.cs`) composes it with the ZeroAlloc.Resilience-generated `AddGitClientResilience<GitCliClient>()`, because Inject cannot see another generator's output. The Mcp project's generated `AddAriadneNETMcpServices()` registers `RuleResolutionHelper` behind `IRuleResolutionHelper`. A new service only needs the attribute; `ServiceRegistrationTests` asserts the Core services resolve.

### Errors

Expected failures are values, not exceptions. The loader, resolver, registry, `TemplateReference`, imports loader, `SemVerRange` and sync all return `Result<T, AriadneError>` (ZeroAlloc.Results). `AriadneError` is a record struct with `Code` (`NotFound`=1, `Invalid`=2, `Unavailable`=3), `Message` and `Detail`. The CLI prints `Error: {Message}`, then the `Detail` unindented on the next line when there is one, and exits 2; only `ariadne sync` indents, listing each failed import with its Detail indented under it. Every `Message` is a full sentence ending in a period. MCP tools surface errors by throwing `McpException`, which the SDK turns into an `IsError` tool result; resources throw `McpProtocolException` with `ResourceNotFound`. Remaining `throw` sites are `ArgumentNullException` guards, embedded-resource packaging bugs, the `GitCliClient` proxy feed, that MCP idiom, and two in `CodeGeneratorService`: the `ArgumentException` for a role the pack lacks is a guard against direct API misuse, since `GenerateOrchestrator` passes only the role inference found, and the `ArgumentOutOfRangeException` arm for an undefined `TypeKind` is unreachable, since the loader rejects one. Code generation stays non-Result until M5. Anything else is a bug.

### Telemetry and resilience

`Ariadne.NET.Telemetry.AriadneTelemetry.SourceName` is `"Ariadne.NET"`. `Telemetry/` holds only `AriadneTelemetry` and the instrumented interfaces `IPackOperations` and `ISyncOperations`; their one implementations sit next to the facades that call them, `PackOperations` in `RuleLoader/` and `SyncOperations` in `Sync/`. Spans are `ariadne.load`, `ariadne.resolve`, `ariadne.get_pack` and `ariadne.sync_import`; the histogram is `ariadne.operation.duration`. The ZeroAlloc.Telemetry proxies are built explicitly behind the public facades, not registered in DI. Git access goes through an internal `IGitClient` wrapped by ZeroAlloc.Resilience (Timeout 60 s per git command, one budget shared by all its attempts; Retry 3 attempts, exponential backoff from 500 ms). Git arguments are an argv list with `--`, import names and paths are contained, symlinks are refused, and URL credentials are redacted in messages and `.git-info.json`.

### Repo's own rules

`.architecture/rules.yaml` governs this repo itself (Core / Host / Tests layers on top of `builtin://clean-architecture`): Core depends on nothing, hosts only orchestrate.

## Conventions enforced by analyzers

- Public APIs expose `IList<T>`/`IDictionary<,>`/`IReadOnlyList<T>`, not `List<T>`/`Dictionary<,>` (MA0016). Rule-schema models use `IList<T>`.
- String-keyed dictionaries and string comparisons need an explicit `StringComparer`/`StringComparison` (MA0002 etc.); the engine uses `OrdinalIgnoreCase` for names.
- One type per file (MA0048) — this is why records/enums each have their own file.
- `ConfigureAwait(false)` on awaits.
- Tests: xUnit + FluentAssertions (+ NSubstitute in `Ariadne.NET.Tests`). Test folders mirror the source namespaces (Abstractions types are tested under `tests/Ariadne.NET.Tests/Abstractions/`); MCP tool tests construct the real services directly rather than via DI.

## Docs

`docs/` (design/plan docs in `docs/plans/`, `docs/how-it-works/`) and `docs-site/docs/` are separate copies that have diverged; the Docusaurus site builds from `docs-site/docs/`. Update both when changing documented behavior.

## Releases

Conventional commits on `main` drive release-please, which opens a release PR using the release bot App token. Merging it tags `vX.Y.Z` and dispatches `ci.yml`, whose `publish-nuget` job pushes `Ariadne.NET` and `Ariadne.NET.Abstractions` to nuget.org. The version comes from `.release-please-manifest.json`; see `RELEASING.md`.
