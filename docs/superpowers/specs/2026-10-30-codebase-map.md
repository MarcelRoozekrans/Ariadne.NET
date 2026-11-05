# Codebase Map — Architecture Governance Engine

**Date:** 2026-10-30
**Branch at mapping time:** `fix/mcp-composite-template`

## Tech stack

- .NET 10 (SDK 10.0.403), C#, nullable enabled
- YamlDotNet (rule packs), Roslyn `Microsoft.CodeAnalysis.CSharp` 5.0 (generation + source parsing)
- System.CommandLine 3.0 **preview** (CLI), ModelContextProtocol 1.1 (MCP stdio + ASP.NET Core HTTP)
- ZInject 0.10.5 (compile-time DI via `[Singleton]`)
- xUnit, FluentAssertions 8, NSubstitute
- Analyzers on every project via `Directory.Build.props` (Meziantou, Roslynator, ErrorProne.NET, Hyperlinq, ZeroAlloc)
- Docs: Docusaurus 3.9 in `docs-site/`, deployed to GitHub Pages

## Layout

| Path | Purpose |
|---|---|
| `src/ArchGov.Core/` | Whole engine; the only packable project (NuGet `ArchGov.Core`) |
| `src/ArchGov.Host.Cli/` | `arch` CLI — one static `XxxCommand.Create` per command |
| `src/ArchGov.Host.Mcp/` | MCP stdio server; tools in `Tools/`, resources in `Resources/` |
| `src/ArchGov.Host.Api/` | MCP over HTTP only (REST removed); references the Mcp project |
| `tests/ArchGov.Core.Tests/` | 224 unit + integration tests |
| `tests/ArchGov.Host.Mcp.Tests/` | 25 MCP tool/resource tests |
| `docs/`, `docs-site/docs/` | Two diverged copies of the explanatory docs; `docs/plans/` holds the March 2026 designs |
| `.architecture/rules.yaml` | The repo governs itself (dogfooding) |

## Entry points

- `src/ArchGov.Host.Cli/Program.cs` — builds a `ServiceCollection`, registers 7 commands: init, rules, roles, generate, validate, lint, sync
- `src/ArchGov.Host.Mcp/Program.cs` — generic host, stdio transport, tools/resources from assembly
- `src/ArchGov.Host.Api/Program.cs` — `WebApplication`, `MapMcp()` at `/mcp`

## Core pipeline

`RulePackResolver` (`builtin://`, `import://`) → `RulePackMerger` → `LayerInferenceEngine` → `DependencyValidator` → `CodeGeneratorService`, orchestrated by `GenerateOrchestrator` / `WorkspaceOrchestrator`. `SourceValidator` and `Linter` parse existing code with Roslyn. `Sync/` clones git repos via the `git` CLI into `.architecture/.cache/git/<name>/` and resolves semver ranges against tags.

## State of the original MVP plan

From `docs/plans/2026-03-10-architecture-governance-engine-design.md` §6: everything v1–v3 is done except **NuGet sync provider** and **CI/CD integration examples**. The design's `overrides.yaml` layer (§3) was never implemented.

## Traps

**Functional gaps**

- **`codeFeatureRules` are never enforced.** They are merged (with `locked` protection) and returned by `get_rules`, but no validator reads `AppliesTo`. The checks CODE-001/CODE-002 describe are hard-coded in `SourceValidator` instead. A user-authored rule in YAML does nothing.
- **CLI exit codes are always 0.** `arch validate` prints `FAIL` and exits 0, including when the target directory does not exist (verified 2026-10-30). Same pattern in `lint`, `sync` and `generate`. This blocks any CI-gate use.
- **No distribution of the hosts.** Cli, Mcp and Api are `IsPackable=false`; the README's `arch …` commands only work as `dotnet run --project …`. There is no dotnet tool, container or MCP package.
- **Output is human text only.** No JSON/SARIF from validate/lint, so CI annotations and IDE integration are not possible.
- `import://` loads only the **first** `*.yaml` in the cache directory (`RulePackResolver.LoadImportedPack`).

**Complexity / hotspots**

- Largest files: `SourceValidator.cs` (290 lines, also a change hotspot: 6 commits), `GitCliSyncProvider.cs` (287, has an MA0051 79-line method and shells out to `git`), `CodeGeneratorService.cs` (265).
- `SemVerRange.cs` holds a second type `SemVer` (MA0048) and a regex flagged MA0009.
- Build carries ~26 pre-existing analyzer warnings; new work should add none.

**Coupling**

- `ArchGov.Host.Api` → `ArchGov.Host.Mcp` project reference (intentional, reuses tools).
- `RuleResolutionHelper` and the CLI's `RulesCommand.ResolveRulesFromFile` both read `.architecture/rules.yaml` relative to the **process working directory**, with different fallbacks (MCP defaults to clean-architecture; CLI throws).

**Markers:** no TODO/FIXME/HACK in `src/` or `tests/`.

**Test baseline (2026-10-30):** 224/224 Core, 25/25 Mcp passing; no known failures.

**Critical files — read fully before changing**

- `src/ArchGov.Core/RuleSchema/*` — the YAML contract every pack and every user depends on
- `src/ArchGov.Core/RuleEngine/RulePackMerger.cs` — merge semantics are documented behaviour
- `src/ArchGov.Core/BuiltinPacks/*.yaml` — shipped templates
- `.github/workflows/publish.yml` — publishes to NuGet on every push to `main`

## Repo/process facts

- Conventional commits throughout (50/50 recent subjects), no commitlint config.
- release-please action with `release-type: simple`, no config/manifest file, no tags yet, no CHANGELOG yet.
- GitVersion Mainline; every push to `main` publishes a NuGet package.
- This local clone has **no git remote**; `gh` cannot be used to detect branch protection.

## Recommended starting points

1. Make the CLI a usable CI gate (exit codes, machine-readable output, a dotnet tool) — touches `Host.Cli/Commands/*`, low risk.
2. Make `codeFeatureRules` real (a rule-evaluation model over `AppliesTo`) — touches `RuleSchema` and `SourceValidator`, both critical/hotspot files; needs design first.
