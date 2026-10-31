# Session State

**Last session:** 2026-10-31
**Branch:** `fix/mcp-composite-template`, pushed to the private repo `MarcelRoozekrans/architecture-governance-engine` (`main` still holds the pre-2026-10-30 state). No PR opened yet; the branch collects Milestone 1 work.

## Current Position

- **Milestone:** 1 — Ariadne.NET identity and family shape [active]
- **Phase:** 1.3 — Family conventions [pending, not started]
- **Last completed:** Phase 1.2 — Package split (design, impact analysis and plan under `docs/superpowers/`, all three plan tasks done, final opus review: 0 Critical, 1 Important and 1 Minor, both fixed)

Phase 1.2 outcome:
- New `src/Ariadne.NET.Abstractions` (PackageId `Ariadne.NET.Abstractions`): the six schema types (`RulePack`, `LayerDefinition`, `ComponentRoleDefinition`, `CodeFeatureRuleDefinition`, `NamingConventions`, `CSharpTypeKind`) and the contracts `IRulePackLoader`, `IRulePackResolver`, `IBuiltinPackRegistry`, in one flat namespace `Ariadne.NET.Abstractions`. No package dependencies, `IsAotCompatible`, its own `README.md` for NuGet.
- The engine `Ariadne.NET` references it and keeps YAML loading, `extends` resolution, the merger, built-in packs, validation, sync and generation. `AddAriadneNETServices()` keeps its name; ZInject registers against interfaces from other assemblies.
- Validation contracts (`ISourceValidator`, `ILinter`, `IDependencyValidator`) stay in the engine until M2.1 settles their shape.
- `tests/Ariadne.NET.AotSmoke` (not packable) proves Abstractions under Native AOT: `dotnet publish tests/Ariadne.NET.AotSmoke -c Release -r linux-x64 -p:PublishAot=true -warnaserror`. Phase 1.4 adds it to CI.
- `dotnet pack` yields exactly `Ariadne.NET` and `Ariadne.NET.Abstractions`; the engine nuspec depends on Abstractions.
- Docs updated in `CLAUDE.md`, `README.md`, `docs/how-it-works/02-architecture.md` and `docs-site/docs/how-it-works/02-architecture.md`.

Test baseline: 227 Ariadne.NET.Tests + 26 Mcp + 10 Cli = 263 passing. A clean (`--no-incremental`) build gives 27 pre-existing analyzer warnings.

## Open Decisions

- The user leans towards an own YAML (and JSON) library. Agreed 2026-10-31: JSON stays on System.Text.Json source generation; an AOT-safe YAML path (a ZeroAlloc YAML library, or YamlDotNet's static generator) is a separate project outside Ariadne. When it exists, only the engine's loader changes. Not scheduled.
- From the roadmap design, none blocking 1.3: whether M3 ships a Roslyn analyzer (M3 brainstorm); code generation's role after M4 (M5); Mnemosyne.NET integration (no plan).

## Blockers

None for Phase 1.3.
- **GitHub Actions are disabled on the repo** on purpose. Phase 1.4 re-enables them and makes the repo public; confirm with the user first.
- The main worktree at `the main worktree` has `fix/mcp-composite-template` checked out at an older commit. Work from a worktree and push with `git push -u origin HEAD:fix/mcp-composite-template`, or `git pull` there first.

## Recommended Next Step

Run `resume-work` → `start-next-phase`. Phase 1.3 (Family conventions, `Surface: Refactor`) is pending with no design spec, so it routes to a phase-scope brainstorm, then `refactor-analysis`, then `writing-plans`.

Inputs for 1.3:
- Goal: ZeroAlloc Results instead of exceptions for expected failures in public engine APIs, a clean ZeroAlloc analyzer run, AOT where dependencies allow (Abstractions already is; the engine is not, because of Roslyn Workspaces and YamlDotNet).
- Look at how Mnemosyne.NET (`MnemosyneError`, `MnemosyneErrorCode`, `ZeroAlloc.Results` in its Abstractions) and Rag.NET shape their Results and error types; an `AriadneError` likely belongs in Abstractions, which would give Abstractions its one allowed dependency, ZeroAlloc.Results.
- Throw sites to start from: `TemplateReference.ParseNames`, `RulePackResolver` (`NotSupportedException` for unknown schemes), `YamlRulePackLoader`, `BuiltinPackRegistry.GetPack`. The 27 baseline warnings include the ZeroAlloc analyzer findings.
