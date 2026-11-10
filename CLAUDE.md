# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A pattern-agnostic architecture governance engine for .NET. Architecture patterns (clean, hexagonal, CQRS, …) are declarative YAML **rule packs**; the engine resolves and merges them, then uses them to generate correctly placed C# components (Roslyn) and to validate existing solutions (semantic type graph). Core principle: *AI generates behavior, the engine governs structure.*

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
dotnet run --project src/Ariadne.NET.Cli -- <init|rules|roles|generate|validate|sync|hook> [...]   # init --agent claude also installs the Claude Code hooks into .claude/settings.json (keeps an existing rules.yaml); validate takes a .sln, .slnx, .slnf or .csproj, default ., and checks every .yaml or .yml directly in the .architecture folder (not imports.yaml or imports.yml, the sync manifest) found from the solution's folder upward, not the working directory's rules.yaml; hook <session-start|pre-tool-use|stop> reads Claude Code hook JSON on stdin
dotnet run --project src/Ariadne.NET.Mcp        # MCP over stdio
dotnet run --project src/Ariadne.NET.Mcp.Http   # MCP over HTTP at /mcp

# Native AOT check for Ariadne.NET.Abstractions: fails on any trim or AOT warning
dotnet publish tests/Ariadne.NET.AotSmoke -c Release -r linux-x64 -p:PublishAot=true -warnaserror

# Docs site (Docusaurus, deployed to GitHub Pages)
cd docs-site && npm ci && npm run start
```

CI (`.github/workflows/ci.yml`) builds and tests on ubuntu and windows, with a coverage gate and a codecov upload on ubuntu. It also runs `aot-smoke` and `pack-validate` (packs the four packages and restores the `tools/release/consumer` sample from a local feed), `commitlint` on pull requests, and `publish-nuget` on a tag dispatch. `.github/workflows/baseline-checks.yml` runs the ci-baseline v1 checks: gitleaks, CodeQL and NuGetAudit. There is no separate lint step: analyzers (Meziantou, Roslynator, ErrorProne.NET, NetFabric.Hyperlinq, ZeroAlloc) run during build via `Directory.Build.props`. `TreatWarningsAsErrors` is on in `Directory.Build.props`, so the build must stay at zero warnings. ZeroAlloc.Analyzers is at 1.12.0.

## Architecture

### Projects

- `Ariadne.NET.Abstractions` — the rule-pack schema (`RulePack`, `LayerDefinition`, `ComponentRoleDefinition`, …) and the pack contracts `IRulePackLoader`, `IRulePackResolver`, `IBuiltinPackRegistry`, all in the one flat namespace `Ariadne.NET.Abstractions`. Packable. Its only dependencies are `ZeroAlloc.Results` 1.4.5 and `ZeroAlloc.Validation` 2.5.0 (which bundles its generator; there is no separate Generator reference), and it is `IsAotCompatible`; `tests/Ariadne.NET.AotSmoke` proves it publishes with Native AOT. It also holds `AriadneError`. Keep it that way: no ZeroAlloc.Inject, no YamlDotNet, no loading logic. The validation contracts stay in the engine until M2 settles their shape.
- `Ariadne.NET` — the engine: YAML loading, `extends` resolution, merging, built-in packs, the semantic validator (`Analysis/`), sync and code generation. Packable; depends on Abstractions. Must have no host dependencies, and no MSBuild: it validates `Compilation`s it is handed.
- `Ariadne.NET.Workspaces` — loads a `.sln`, `.slnx`, `.slnf` or `.csproj` with `MSBuildWorkspace` (`ISolutionLoader`) and runs the engine's validator on it (`ISolutionValidator`). Packable; with `Ariadne.NET.Testing`, the only project that references `Microsoft.CodeAnalysis.Workspaces.MSBuild` (`Microsoft.Build.Locator` comes in through Workspaces). It also holds `IPackSetValidator`: it finds the `.architecture` folder from the solution's folder upward, checks every `.yaml` or `.yml` directly in it as its own pack, except `imports.yaml` and `imports.yml`, the `ariadne sync` manifest (an error that does not already name the file is prefixed with `In pack file '<name>': `, each violation carries `Pack`) and merges them into one report. Every project that references it also needs `Microsoft.Build.Framework` 17.14.28 with `ExcludeAssets="runtime"`, or the build fails with `MSBL001`; the CLI, MCP hosts, tests, CI consumer sample and `tools/perf` carry it. No other `Microsoft.Build*` assembly may land in an output folder. Ariadne never restores.
- `Ariadne.NET.Testing` — `ArchitectureCheck.Run`, `RunAsync`, `AssertValid` and `AssertValidAsync` run the pack-set check from a unit test in any framework; failures are `ArchitectureCheckException` (could not run) and its subtype `ArchitectureViolationException` (message is the text report, `Report` holds the data). Packable, depends on Workspaces, net10 only. Consumers add no `Microsoft.Build.Framework` line: `buildTransitive/Ariadne.NET.Testing.targets` removes its runtime copy before MSBuildLocator's check, and the package references `Microsoft.CodeAnalysis.Workspaces.MSBuild` itself with `PrivateAssets="none"` so the BuildHost folders reach the consumer. Projects in this repo reference it as a project, which skips `buildTransitive`, so `tests/Ariadne.NET.Testing.Tests` keeps its own `Microsoft.Build.Framework` reference. `tools/release/inspect_packages.py` lets Workspaces and Testing depend on MSBuild packages and nothing else.
- `Ariadne.NET.Hooks` — the Claude Code hooks behind `ariadne hook <event>`: `HookHost` runs the `IHook` for session-start, pre-tool-use or stop. Host layer, not packable. **Fail open:** a hook exits 0 for every outcome and a failure becomes one `ariadne hook: ...` line on stderr with empty stdout, because a non-zero exit blocks the agent's tool call (only a missing event argument is a usage error). `HookCommand` must let nothing escape.
- `Ariadne.NET.Cli` — System.CommandLine 3.0 (preview) CLI; `validate` is async on `IPackSetValidator`, and a cancelled run prints `Cancelled.` and exits 2; one static `XxxCommand.Create(IServiceProvider)` per command in `Commands/`, wired in `CliRootCommand`. Commands return `ExitCodes` (0 success, 1 violations, 2 error); `CliRootCommand.InvokeAsync` turns uncaught exceptions into 2. `hook` is the exception to the exit codes: it always returns 0, apart from the missing-argument usage error, and speaks UTF-8 without a BOM on its standard streams. `tests/Ariadne.NET.Cli.Tests` changes the process working directory, so it runs without test parallelization.
- `Ariadne.NET.Mcp` — MCP stdio server. Tools are static methods on `[McpServerToolType]` classes in `Tools/`; resources in `Resources/` (`ariadne://packs/{name}`). Services are injected as method parameters.
- `Ariadne.NET.Mcp.Http` — **not a REST API anymore** (the REST controllers were removed). It is only the HTTP transport for the MCP tools, reusing the tool/resource assemblies from `Ariadne.NET.Mcp`.

### Pipeline

`RulePackResolver` → `RulePackMerger` → `LayerInferenceEngine` → `CodeGeneratorService`, wired by `GenerateOrchestrator` (components) and `WorkspaceOrchestrator` (solution scaffolding). Validation is a separate path: `ISolutionValidator` (Workspaces) loads the projects, then `IArchitectureValidator` (`Analysis/`) builds a `TypeGraph` per compilation, one edge per type reference somebody wrote, and checks it with `LayerRules`, `ForbidRules`, `AllowOnlyRules`, `RequireRules` and `RoleRules` over a selector-based `LayerMap`. `var` and unused usings give no edge, generated code is never a source, and a project is checked once per target framework. A layer violation is one per source and target type, with the layer id (or the joined ids for an overlap), a rule id, `ROLE-<Name>`, `CODE-001` or `CODE-002` as rule id, and the layer's or rule's `because`. `docs/perf/phase-2.1-baseline.md` holds the load and check baseline from `tools/perf`, with the phase 2.2 re-measurement in its own section.

- **Resolution:** a user pack (usually `.architecture/rules.yaml`) lists `extends:` references. `builtin://<name>` loads an embedded pack from `src/Ariadne.NET/BuiltinPacks/*.yaml` (registered via `EmbeddedResource`; adding a YAML file there adds a template). `import://<name>` loads from `.architecture/.cache/git/<name>/` and requires a working directory — populated by `ariadne sync` from `.architecture/imports.yaml` (`Sync/`: git CLI provider, semver tag ranges). Anything else is an `Invalid` Result. The user pack is merged last.
- **Loading and validation:** `PackYamlReader` reads a schema 2 pack strictly. It walks YamlDotNet's representation model by hand against a closed key set per mapping and collects every problem as `<location>:<line>:<column>: <message>`; a missing `schema: 2` is the one v1 hint, linking the docs page's `#migrating-from-v1` anchor. Then the member rules run through the `*Validator` types that ZeroAlloc.Validation generates in Abstractions, as a backstop for packs built in code. After every resolve, `RulePackReferenceCheck` checks cross-references on the merged pack: duplicate layer or role names, duplicate layer and rule ids, an empty layer or rule id (a pack built in code), unknown layers in `allowedDependencies` and in rule targets, a role's unknown layer, an unknown `interfaceRole`. Both report `Rule pack '{name}' is invalid.` with one `path: reason` line per failure as Detail, built by `RulePackError`. A role-only pack (`cqrs`, `ddd-tactical`) has no layers and resolves on its own, but should be combined with a base pack that declares the layers its roles name; when the merged pack declares at least one layer, a role naming an undeclared layer is an `Invalid` error.
- **Merge semantics** (`RulePackMerger`, errors only, every problem collected): packs merge parent first, then the child. A layer or rule id defined by two packs is a conflict; a child retargets an inherited layer's `namespaces` and `assemblies` (an entry with only `name` and selectors) and appends `exceptions` to an inherited rule (an entry with only `id` and `exceptions`), and an amendment of a `locked` rule fails. Component roles merge by name, later wins; naming conventions: last pack wins. Composite template expressions like `clean-architecture+cqrs` are parsed by `TemplateReference` into one `builtin://` reference per name; both `ariadne init` and the MCP `template` parameter use it. Two layer-defining packs, such as `clean-architecture+microservice`, do not compose.
- **Rule auto-detection in MCP:** `RuleResolutionHelper` uses an explicit `template` argument, else `.architecture/rules.yaml` in the current directory, else falls back to `clean-architecture`. The CLI instead requires `.architecture/rules.yaml` (created by `ariadne init`).

### Dependency injection

Registration is compile-time via **ZeroAlloc.Inject** 1.11.6: classes are annotated `[Singleton]` and the generator emits the internal `AddAriadneNETGeneratedServices()` (Core). The hand-written public `AddAriadneNETServices()` (`AriadneNETServiceCollectionExtensions.cs`) composes it with the ZeroAlloc.Resilience-generated `AddGitClientResilience<GitCliClient>()`, because Inject cannot see another generator's output. The Workspaces project registers `ISolutionLoader`, `ISolutionValidator` and `IPackSetValidator` with plain `AddSingleton` in `AddAriadneNETWorkspaces()`, which callers use next to `AddAriadneNETServices()`. The Mcp project's generated `AddAriadneNETMcpServices()` registers `RuleResolutionHelper` behind `IRuleResolutionHelper`. A new service only needs the attribute; `ServiceRegistrationTests` asserts the Core services resolve.

### Errors

Expected failures are values, not exceptions. The loader, resolver, registry, `TemplateReference`, imports loader, `SemVerRange` and sync all return `Result<T, AriadneError>` (ZeroAlloc.Results). `AriadneError` is a record struct with `Code` (`NotFound`=1, `Invalid`=2, `Unavailable`=3), `Message` and `Detail`. The CLI prints `Error: {Message}`, then the `Detail` unindented on the next line when there is one, and exits 2; only `ariadne sync` indents, listing each failed import with its Detail indented under it. Every `Message` is a full sentence ending in a period. MCP tools surface errors by throwing `McpException`, which the SDK turns into an `IsError` tool result; resources throw `McpProtocolException` with `ResourceNotFound`. Remaining `throw` sites are `ArgumentNullException` guards, embedded-resource packaging bugs, the `GitCliClient` proxy feed, that MCP idiom, and two in `CodeGeneratorService`: the `ArgumentException` for a role the pack lacks is a guard against direct API misuse, since `GenerateOrchestrator` passes only the role inference found, and the `ArgumentOutOfRangeException` arm for an undefined `TypeKind` is unreachable, since the loader rejects one. Code generation stays non-Result until M5. Anything else is a bug.

### Telemetry and resilience

`Ariadne.NET.Telemetry.AriadneTelemetry.SourceName` is `"Ariadne.NET"`. `Telemetry/` holds only `AriadneTelemetry` and the instrumented interfaces `IPackOperations` and `ISyncOperations`; their one implementations sit next to the facades that call them, `PackOperations` in `RuleLoader/` and `SyncOperations` in `Sync/`. Spans are `ariadne.load`, `ariadne.resolve`, `ariadne.get_pack` and `ariadne.sync_import`; the histogram is `ariadne.operation.duration`. The ZeroAlloc.Telemetry proxies are built explicitly behind the public facades, not registered in DI. Git access goes through an internal `IGitClient` wrapped by ZeroAlloc.Resilience (Timeout 60 s per git command, one budget shared by all its attempts; Retry 3 attempts, exponential backoff from 500 ms). Git arguments are an argv list with `--`, import names and paths are contained, symlinks are refused, and URL credentials are redacted in messages and `.git-info.json`.

### Repo's own rules

`.architecture/rules.yaml` is a standalone schema 2 pack that governs this repo itself, selecting by assembly: Core, Workspaces, Host (CLI, hooks, MCP servers and the testing package) and Tests layers, plus `forbid` rules that keep Core free of the hosts, the workspace loader and host frameworks. CI runs `ariadne validate` on the repo's own solution with it; `validate` checks every pack file in `.architecture/`, so a second pack file added there is checked too, and a pack meant to be imported must be a `builtin://` or `import://` reference, not a file in that folder.

## Conventions enforced by analyzers

- Public APIs expose `IList<T>`/`IDictionary<,>`/`IReadOnlyList<T>`, not `List<T>`/`Dictionary<,>` (MA0016). Rule-schema models use `IList<T>`.
- String-keyed dictionaries and string comparisons need an explicit `StringComparer`/`StringComparison` (MA0002 etc.); the engine uses `OrdinalIgnoreCase` for names.
- One type per file (MA0048) — this is why records/enums each have their own file.
- `ConfigureAwait(false)` on awaits.
- Tests: xUnit + FluentAssertions (+ NSubstitute in `Ariadne.NET.Tests`). Test folders mirror the source namespaces (Abstractions types are tested under `tests/Ariadne.NET.Tests/Abstractions/`, the validator under `Analysis/`); `tests/Ariadne.NET.Workspaces.Tests` loads the fixture solutions in `tests/fixtures`; `tests/Ariadne.NET.Testing.Tests` covers `ArchitectureCheck` (xUnit, project reference, so its own `Microsoft.Build.Framework` line); MCP tool tests construct the real services directly rather than via DI.

## Docs

The Docusaurus site builds from `docs-site/docs/`, the source of the user-facing docs (`docs/features.md` is an older copy that is no longer updated); `docs/` otherwise holds design and plan documents, perf notes and planning state. A change to the pack format also changes `docs-site/static/schema/pack.schema.json`; the tests in `tests/Ariadne.NET.Tests/PackSchema` fail when the schema and the loader disagree on a listed case; add a case there for every new key or rule.

## Releases

Conventional commits on `main` drive release-please, which opens a release PR using the release bot App token. Merging it tags `vX.Y.Z` and dispatches `ci.yml`, whose `publish-nuget` job pushes `Ariadne.NET`, `Ariadne.NET.Abstractions`, `Ariadne.NET.Workspaces` and `Ariadne.NET.Testing` to nuget.org. The version comes from `.release-please-manifest.json`; see `RELEASING.md`.
