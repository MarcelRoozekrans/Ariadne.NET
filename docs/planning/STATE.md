# Session State

**Last session:** 2026-11-03
**Branch:** `fix/mcp-composite-template`, pushed to the private repo `MarcelRoozekrans/architecture-governance-engine` (`main` still holds the pre-2026-10-30 state). No PR opened yet; the branch collects Milestone 1 work.

## Current Position

- **Milestone:** 1 — Ariadne.NET identity and family shape [active]
- **Phase:** 1.4 — Publish [planned 2026-11-03; spec `docs/superpowers/specs/2026-10-31-phase-1.4-publish-design.md`, plan `docs/superpowers/plans/2026-11-03-phase-1.4-publish.md`, 13 tasks, none started]
- **Last completed:** Phase 1.3 — Family conventions, completed 2026-10-31. All 11 plan tasks executed subagent-driven, each with task review and fix loop, then a whole-branch final review, one fix wave and a clean scoped re-review.

Phase 1.3 outcome:
- Pack pipeline and sync return `Result<T, AriadneError>`; CLI maps errors to exit 2 via `ErrorOutput`, MCP via `ToolError`/`ErrorText`.
- ZeroAlloc.Inject 1.11.6, Analyzers 1.11.3 with `TreatWarningsAsErrors`, Validation 2.5.0 on the Abstractions schema types, Telemetry 1.12.7 (source `Ariadne.NET`), Resilience 3.4.0 on an internal `IGitClient`.
- Sync hardened: git argv list with `--`, contained import names and paths, symlink and `.git` refusal, staged cache swap, url credential redaction.
- Verified at head: `dotnet build --no-incremental` 0 warnings; `dotnet test` 474 passing (413 + 41 Mcp + 20 Cli); AotSmoke publish clean; `dotnet pack` gives exactly `Ariadne.NET` and `Ariadne.NET.Abstractions`, the Abstractions nuspec lists only ZeroAlloc.Results and ZeroAlloc.Validation.

## Open Decisions

- ZeroAlloc.Analyzers goes to 1.12.0 in 1.4, decided 2026-10-31; it arrives with the merge of `main` in plan task 1.
- The execution method for the 1.4 plan has not been chosen yet: subagent-driven or native.
- An own YAML library: a separate, unscheduled project outside Ariadne (agreed 2026-10-31).
- From the roadmap design, none blocking: whether M3 ships a Roslyn analyzer; code generation's role after M4; Mnemosyne.NET integration.

## Blockers

- **Upstream issue not filed.** ZeroAlloc.Resilience 3.4.0 reports the total timeout as `ResilienceError` with PolicyType "Retry", and a timeout during the backoff delay carries the previous attempt's error. Filing on `ZeroAlloc-Net/ZeroAlloc.Resilience` was blocked by the session's permission policy; the user files it or allows it. Ariadne maps it correctly via `InnerException is OperationCanceledException` in `Sync/SyncOperations.cs`.
- **GitHub Actions are disabled on the repo** on purpose. Phase 1.4 re-enables them and makes the repo public; confirm with the user first.
- The main worktree at `the main worktree` has `fix/mcp-composite-template` checked out at an older commit. Work from a worktree, and push with `git push -u origin HEAD:fix/mcp-composite-template`.

## Added 2026-11-03

- The owner added a CI security and quality baseline to Phase 1.4 (gitleaks per PR, NuGetAudit on transitive packages, CodeQL, CycloneDX SBOM, codecov coverage floor). It is in the roadmap and in the Goals of the 1.4 design spec; the plan must cover it.
- Phase 2.4 now also covers structural security rules (APIs and namespaces forbidden per layer), and a new Phase 6.4 adds an ISO/IEC 25010 maintainability pack.
- The owner wants the same baseline in all their repos, personal and org. That is the family reusable workflow the 1.4 design lists as a non-goal: its own project, outside this repo.

## Unblocked 2026-11-03: ci-baseline v1

`MarcelRoozekrans/ci-baseline` v1.0.0 is released, and the major tag `v1` points at the same commit, `6faa4c5`. Phase 1.4 is unblocked.

- What it provides: the reusable workflows `baseline.yml`, with the required checks `baseline / gitleaks`, `baseline / codeql (csharp)` and `baseline / nuget-audit`, and `history-scan.yml`, plus the actions `actions/coverage-gate`, an 80% line floor, and `actions/sbom`, a CycloneDX SBOM on the release. Its README is the adoption guide: caller jobs, the `codecov.yml` block, the `Directory.Build.props` audit properties and the ruleset's required check names.
- Consequence for the 1.4 design: the spec lists a shared reusable workflow as a non-goal, in Non-goals and option C, and plans its own per-repo baseline. That workflow now exists. Before writing the 1.4 plan, update the spec so 1.4 adopts `ci-baseline@v1` instead of duplicating it; confirm with the user.

## Decided 2026-11-03: ci-baseline adoption

The spec now adopts `ci-baseline@v1`. The owner chose: the `baseline` caller in its own `baseline-checks.yml`; the pre-public history scan run locally with gitleaks 8.30.1, so Actions stay off until it passes; the `main` ruleset added after the Milestone 1 merge, once `main` has a codecov upload. Merged line coverage measured with the gate's exclusions: 89.4 percent, so no floor override. `main` now has 33 Renovate commits the branch lacks; plan task 1 merges them.

## Recommended Next Step

The owner reviews the 1.4 plan and picks the execution method, then execution starts at task 1: delete `publish.yml`, merge `origin/main` and re-apply the Renovate bumps to the renamed projects. Tasks 10 to 13 each stop for the owner: history findings, going public, merging, releasing.

Commit rules: conventional commits; no `Co-Authored-By` trailer or session link (owner's global CLAUDE.md); no nested parentheses in commit bodies (release-please).
