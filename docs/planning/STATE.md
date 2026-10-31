# Session State

**Last session:** 2026-10-31
**Branch:** `fix/mcp-composite-template`, pushed to the private repo `MarcelRoozekrans/architecture-governance-engine` (`main` still holds the pre-2026-10-30 state). No PR opened yet; the branch collects Milestone 1 work.

## Current Position

- **Milestone:** 1 — Ariadne.NET identity and family shape [active]
- **Phase:** 1.2 — Package split [pending, not started]
- **Last completed:** Phase 1.1 — Rename to Ariadne.NET (design, impact analysis and plan under `docs/superpowers/`, all four plan tasks done, final review fixes committed)

Phase 1.1 outcome:
- Projects: `src/Ariadne.NET` (engine, PackageId `Ariadne.NET`), `src/Ariadne.NET.Mcp`, `src/Ariadne.NET.Cli`, `src/Ariadne.NET.Mcp.Http`; tests `Ariadne.NET.Tests`, `Ariadne.NET.Mcp.Tests`, `Ariadne.NET.Cli.Tests`; solution `src/Ariadne.NET.slnx`.
- Namespace = assembly = package ID = `Ariadne.NET.*`, as in Rag.NET and Mnemosyne.NET.
- ZInject derives method names from the assembly name with the dots removed: `AddAriadneNETServices()`, `AddAriadneNETMcpServices()`. Renaming or splitting an assembly in 1.2 renames these.
- MCP resource `ariadne://packs/{name}`; CLI wording `ariadne <verb>`. Docs note that until M3.3 this means `dotnet run --project src/Ariadne.NET.Cli --`.
- `git grep -i archgov -- . ':!docs/plans' ':!docs/planning' ':!docs/superpowers'` is empty.
- Left alone on purpose: the repo name, the Docusaurus `url`/`baseUrl`/`projectName`, and the prose "architecture governance engine", which describes the product rather than naming it.

Test baseline: 225 Ariadne.NET.Tests + 26 Mcp + 10 Cli = 261 passing. A clean (`--no-incremental`) build gives 27 pre-existing analyzer warnings.

## Open Decisions

Recorded in the roadmap design's Open Questions; none block Phase 1.2:
- Whether M3 also ships a Roslyn analyzer (settle in M3's milestone brainstorm)
- The role of code generation after M4 (settle in M5)
- Mnemosyne.NET integration (not planned until a concrete use appears)

## Blockers

None for Phase 1.2.
- **GitHub Actions are disabled on the repo** on purpose. Phase 1.4 re-enables them and makes the repo public; confirm with the user first.
- The main worktree at `the main worktree` has `fix/mcp-composite-template` checked out, at the older commit `d7efa92`. Run `git pull` there, or work from a worktree and push with `HEAD:fix/mcp-composite-template`.

## Recommended Next Step

Run `start-next-phase`. Phase 1.2 (Package split, `Surface: Refactor`) is pending and has no design spec, so it routes to a phase-scope brainstorm, then `refactor-analysis`, then `writing-plans`.

Inputs from the roadmap design (`docs/superpowers/specs/2026-10-30-roadmap-design.md`):
- `Ariadne.NET.Abstractions` gets the schema and the loader.
- `Ariadne.NET` stays the engine.
- The surfaces (Mcp, Cli, Mcp.Http) sit on top.
- Abstractions is the AOT target; the engine may not be.
- roslyn-codelens will consume Abstractions in M6.1.

Look at Rag.NET's and Mnemosyne.NET's Abstractions packages for the family shape.
