# Session State

**Last session:** 2026-11-04
**Repository:** `MarcelRoozekrans/Ariadne.NET`, public since 2026-11-03. The old private repository `MarcelRoozekrans/architecture-governance-engine` is frozen and stays private. Local clones: remote `ariadne` is the new repository, `origin` the old one; push with `git push ariadne HEAD:<branch>`.

## Current Position

- **Milestone:** 1 — Ariadne.NET identity and family shape [complete 2026-11-04]; audit PASS in `docs/superpowers/reviews/2026-11-04-milestone-1-audit.md`
- **Phase:** 1.4 — Publish [complete 2026-11-04]
- **Active milestone:** 2 — Rule engine v2 [active since 2026-11-04]; design `docs/superpowers/specs/2026-11-04-milestone-2-design.md`, `MILESTONE.md` written. All six phases pending.

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

Once the Milestone 2 design PR is merged, run project-orchestration `start-next-phase`: it routes Phase 2.1, Semantic dependency analysis, to a phase-scope brainstorm, then writing-plans. Branch from `ariadne/main`.

User decisions of 2026-11-04 for Milestone 2: namespace and assembly glob selectors; solutions load through MSBuildWorkspace; the syntax-only `SourceValidator` is replaced, not kept. Claude's calls are listed in the design spec for review.

Commit rules: conventional commits; no `Co-Authored-By` trailer or session link; no nested parentheses in commit bodies.
