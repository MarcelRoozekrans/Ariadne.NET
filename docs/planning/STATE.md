# Session State

**Last session:** 2026-10-30
**Branch:** `fix/mcp-composite-template`, pushed to the private repo `MarcelRoozekrans/architecture-governance-engine` (created 2026-10-30; `main` holds the pre-session state)

## Current Position

- **Milestone:** 1 — Ariadne.NET identity and family shape [active]
- **Phase:** 1.1 — Rename to Ariadne.NET [pending, not started]
- **Last completed:** project roadmap planned (`docs/planning/ROADMAP.md`, M1–M6) from `docs/superpowers/specs/2026-10-30-roadmap-design.md`

Done this session, all committed on the branch above:
- `fix:` composite templates (`clean-architecture+cqrs`) now work in the MCP `template` parameter (`TemplateReference` in Core)
- `fix:` CLI exit codes — 0 success, 1 violations, 2 error (`ExitCodes`, `CliRootCommand`, new `tests/ArchGov.Host.Cli.Tests`)
- `docs:` README and both docs trees cleaned of the removed REST API and `AddArchGovCore()`; `CLAUDE.md` added
- Conventions (`docs/planning/CONVENTIONS.md`), codebase map and roadmap design under `docs/`

Test baseline: 224 Core + 25 Mcp + 9 Cli tests, all passing. ~26 pre-existing analyzer warnings; new work adds none.

## Open Decisions

Recorded in the roadmap design's Open Questions — none block Phase 1.1:
- Whether M3 also ships a Roslyn analyzer (settle in M3's milestone brainstorm)
- The role of code generation after M4 (settle in M5)
- Mnemosyne.NET integration (not planned until a concrete use appears)

## Blockers

None for Phase 1.1. **GitHub Actions are disabled on the repo** on purpose: `publish.yml` would otherwise publish `ArchGov.Core` to GitHub Packages under the old name on every push to `main`. Phase 1.4 re-enables them after the rename, and makes the repo public (confirm with the user first). No PR has been opened for the branch yet.

## Recommended Next Step

Run `start-next-phase`: Phase 1.1 is `pending` with no design spec, so it routes to brainstorming at phase scope, then — because `Surface: Refactor` — to `refactor-analysis` before `writing-plans`. Key inputs for the rename: product name `Ariadne.NET`, CLI/tool command `ariadne`, nothing published under the old names (no NuGet package, no GitHub repo), historical `docs/plans/*` keep the old name.
