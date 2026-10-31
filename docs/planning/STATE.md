# Session State

**Last session:** 2026-10-31
**Branch:** `fix/mcp-composite-template`, pushed to the private repo `MarcelRoozekrans/architecture-governance-engine` (`main` still holds the pre-2026-10-30 state). No PR opened yet; the branch collects Milestone 1 work.

## Current Position

- **Milestone:** 1 — Ariadne.NET identity and family shape [active]
- **Phase:** 1.4 — Publish [pending, no design spec yet]
- **Last completed:** Phase 1.3 — Family conventions, completed 2026-10-31. All 11 plan tasks executed subagent-driven, each with task review and fix loop, then a whole-branch final review, one fix wave and a clean scoped re-review.

Phase 1.3 outcome:
- Pack pipeline and sync return `Result<T, AriadneError>`; CLI maps errors to exit 2 via `ErrorOutput`, MCP via `ToolError`/`ErrorText`.
- ZeroAlloc.Inject 1.11.6, Analyzers 1.11.3 with `TreatWarningsAsErrors`, Validation 2.5.0 on the Abstractions schema types, Telemetry 1.12.7 (source `Ariadne.NET`), Resilience 3.4.0 on an internal `IGitClient`.
- Sync hardened: git argv list with `--`, contained import names and paths, symlink and `.git` refusal, staged cache swap, url credential redaction.
- Verified at head: `dotnet build --no-incremental` 0 warnings; `dotnet test` 474 passing (413 + 41 Mcp + 20 Cli); AotSmoke publish clean; `dotnet pack` gives exactly `Ariadne.NET` and `Ariadne.NET.Abstractions`, the Abstractions nuspec lists only ZeroAlloc.Results and ZeroAlloc.Validation.

## Open Decisions

- ZeroAlloc.Analyzers 1.12.0 exists; the 1.3 spec pinned 1.11.x, so it stayed at 1.11.3. Ask the user whether to bump in 1.4.
- An own YAML library: a separate, unscheduled project outside Ariadne (agreed 2026-10-31).
- From the roadmap design, none blocking: whether M3 ships a Roslyn analyzer; code generation's role after M4; Mnemosyne.NET integration.

## Blockers

- **Upstream issue not filed.** ZeroAlloc.Resilience 3.4.0 reports the total timeout as `ResilienceError` with PolicyType "Retry", and a timeout during the backoff delay carries the previous attempt's error. Filing on `ZeroAlloc-Net/ZeroAlloc.Resilience` was blocked by the session's permission policy; the user files it or allows it. Ariadne maps it correctly via `InnerException is OperationCanceledException` in `Sync/SyncOperations.cs`.
- **GitHub Actions are disabled on the repo** on purpose. Phase 1.4 re-enables them and makes the repo public; confirm with the user first.
- The main worktree at `the main worktree` has `fix/mcp-composite-template` checked out at an older commit. Work from a worktree, and push with `git push -u origin HEAD:fix/mcp-composite-template`.

## Recommended Next Step

Run `start-next-phase`: Phase 1.4 (Publish, Surface Infra) has no design spec, so it routes to `superpowers:brainstorming` at phase scope. Inputs: CI with build, tests, the AotSmoke publish (`dotnet publish tests/Ariadne.NET.AotSmoke -c Release -r linux-x64 -p:PublishAot=true -warnaserror`) and codecov; release-please first release; docs site; Family README section; making the repo public and re-enabling Actions (confirm with the user).

Commit rules: conventional commits; no `Co-Authored-By` trailer or session link (owner's global CLAUDE.md); no nested parentheses in commit bodies (release-please).
