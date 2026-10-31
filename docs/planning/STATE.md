# Session State

**Last session:** 2026-10-31
**Branch:** `fix/mcp-composite-template`, pushed to the private repo `MarcelRoozekrans/architecture-governance-engine` (`main` still holds the pre-2026-10-30 state). No PR opened yet; the branch collects Milestone 1 work.

## Current Position

- **Milestone:** 1 — Ariadne.NET identity and family shape [active]
- **Phase:** 1.3 — Family conventions [active, plan written, execution not started]
- **Last completed:** Phase 1.3 design, impact analysis and plan, all approved by the user on 2026-10-31.

Phase 1.3 artifacts:
- Design: `docs/superpowers/specs/2026-10-31-phase-1.3-family-conventions-design.md`
- Impact analysis: `docs/superpowers/specs/2026-10-31-phase-1.3-family-conventions-impact-analysis.md` (66 files, High risk, 9 groups)
- Plan: `docs/superpowers/plans/2026-10-31-phase-1.3-family-conventions.md` (11 tasks; Tasks 6, 9 and 10 are marked `**Model:** opus`)

Decided with the user on 2026-10-31 (all recorded in the design):
- Results scope covers the pack pipeline and sync only. Validation, lint, generation and layer inference wait for M2 and M5.
- `AriadneError` follows the Mnemosyne style (a record struct plus an `AriadneErrorCode` enum: NotFound=1, Invalid=2, Unavailable=3), in Abstractions.
- Zero warnings, with `TreatWarningsAsErrors`.
- Use as many ZeroAlloc libraries as genuinely fit:
  - Inject: replaces the dead ZInject package.
  - Analyzers 1.11.
  - Validation, as attributes on the Abstractions schema types (the Rag.NET precedent). Abstractions then depends on Results and Validation.
  - Telemetry, through internal engine wrappers (the Minos pattern).
  - Resilience, on an internal `IGitClient`.
  - Deferred: Cache to 6.2, Serialisation to 3.2, Specification to 2.2/2.3, TestHelpers to 2.1. Task 11 writes these notes into ROADMAP.md.
- The phase-assumptions summary was presented and accepted. The execution method is **subagent-driven development**.

Test baseline: 227 Ariadne.NET.Tests + 26 Mcp + 10 Cli = 263 passing. A clean build gives 27 warnings, and the plan's Task 2 takes this to 0.

## Open Decisions

- An own YAML library: a separate, unscheduled project outside Ariadne (agreed 2026-10-31).
- From the roadmap design, none blocking: whether M3 ships a Roslyn analyzer; code generation's role after M4; Mnemosyne.NET integration.

## Blockers

None.
- **GitHub Actions are disabled on the repo** on purpose. Phase 1.4 re-enables them and makes the repo public; confirm with the user first.
- The main worktree at `the main worktree` has `fix/mcp-composite-template` checked out at an older commit. Work from a worktree, and push with `git push -u origin HEAD:fix/mcp-composite-template`.

## Recommended Next Step

Execute the Phase 1.3 plan with `superpowers:subagent-driven-development`, starting at Task 1 (ZInject to ZeroAlloc.Inject). `list-phase-assumptions` and the method choice are already done, so do not re-ask them. When all 11 tasks are checked off and the final review passes, run `complete-phase` for 1.3 and then route to Phase 1.4 (Publish, Surface Infra).

Commit rules: conventional commits; no `Co-Authored-By` trailer or session link (owner's global CLAUDE.md); no nested parentheses in commit bodies (release-please).
