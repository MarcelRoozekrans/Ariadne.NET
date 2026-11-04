# Session State

**Last session:** 2026-11-04
**Repository:** `MarcelRoozekrans/Ariadne.NET`, public since 2026-11-03. The old private repository `MarcelRoozekrans/architecture-governance-engine` is frozen and stays private. Local clones: remote `ariadne` is the new repository, `origin` the old one; push with `git push ariadne HEAD:<branch>`.

## Current Position

- **Milestone:** 1 — Ariadne.NET identity and family shape [complete 2026-11-04]; audit PASS in `docs/superpowers/reviews/2026-11-04-milestone-1-audit.md`
- **Phase:** 1.4 — Publish [complete 2026-11-04]
- **Active milestone:** 2 — Rule engine v2 [active since 2026-11-04]; design `docs/superpowers/specs/2026-11-04-milestone-2-design.md`, `MILESTONE.md` written. All six phases pending.
- **Phase 2.1 — Semantic dependency analysis:** active since 2026-11-04. Design and plan merged in PR #23. Execution runs subagent-driven on branch `feat/phase-2.1-semantic-analysis`, pushed with `git push ariadne HEAD:feat/phase-2.1-semantic-analysis`. Design `docs/superpowers/specs/2026-11-04-phase-2.1-semantic-dependency-analysis-design.md`; plan `docs/superpowers/plans/2026-11-04-phase-2.1-semantic-dependency-analysis.md`, 17 tasks.

Phase 2.1 decisions by the owner, 2026-11-04: bound syntax walk for the graph; MCP loads the workspace on every call; one violation per source type and target type with a reference count; every C# project the given path names is checked, with `.slnf` for selection; the Unix test attributes go to `tests/Shared/`, not ZeroAlloc.TestHelpers; execution is subagent-driven. Issue #1 was reopened after PR #22 auto-closed it; the implementation PR closes it.

Release 0.1.0, 2026-11-04:
- The owner added the release bot App, `RELEASE_BOT_CLIENT_ID`, `RELEASE_BOT_PRIVATE_KEY`, `NUGET_USER`, the Trusted Publishing policy for new ids and the `Ariadne.NET` prefix reservation.
- Release PR #18 proposed 0.1.0; its changelog holds all 62 `feat:` and `fix:` commits of `main`. Only two merge commits were unparsable, as expected. Squash-merged as `9f10599`.
- Release Please tagged `v0.1.0` and dispatched `ci.yml` run 41798631236: build, aot-smoke, pack-validate and publish-nuget green. Both `.nupkg` and both `.snupkg` were pushed to nuget.org.
- `Ariadne.NET` and `Ariadne.NET.Abstractions` 0.1.0 resolve on nuget.org: `dotnet add package` of both into a fresh console app restores and builds with 0 warnings.
- The GitHub release carries `Ariadne.NET.0.1.0.nupkg`, `Ariadne.NET.Abstractions.0.1.0.nupkg`, both `.snupkg` and `Ariadne.NET-v0.1.0.cdx.json`.

## Open Decisions

- An own YAML library: a separate, unscheduled project outside Ariadne (agreed 2026-10-31).
- From the roadmap design, none blocking: whether M3 ships a Roslyn analyzer; code generation's role after M4; Mnemosyne.NET integration.

## Blockers

- **actionlint false positives:** actionlint 1.7.12 reports two errors on `actions/create-github-app-token@v3` `client-id`, an actionlint metadata bug tracked as rhysd/actionlint#648 and #669. Expect exactly these two until it ships a fix.

## Follow-ups

- `MarcelRoozekrans/Ariadne.NET#1`: `SourceValidator` misses usings inside a file-scoped namespace and scans `bin/` and `obj/`.
- Open Renovate and Dependabot PRs on the new repository: review them now that 0.1.0 is out.

## Recommended Next Step

Once PR #23 is merged, run project-orchestration `start-next-phase`: it routes Phase 2.1 to `list-phase-assumptions`, then execution. Execute the plan with superpowers:subagent-driven-development on a new branch `feat/phase-2.1-semantic-analysis` from `ariadne/main`, in its own worktree, pushed with `git push ariadne HEAD:feat/phase-2.1-semantic-analysis`. Task 2 is a spike whose outcome goes into the design spec before Task 10. Mark the phase `active` in ROADMAP.md and MILESTONE.md when execution starts.

User decisions of 2026-11-04 for Milestone 2: namespace and assembly glob selectors; solutions load through MSBuildWorkspace; the syntax-only `SourceValidator` is replaced, not kept. Claude's calls are listed in the design specs for review.

Commit rules: conventional commits; no `Co-Authored-By` trailer or session link; no nested parentheses in commit bodies.
