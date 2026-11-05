# Session State

**Last session:** 2026-11-05
**Repository:** `MarcelRoozekrans/Ariadne.NET`, public since 2026-11-03. The old private repository `MarcelRoozekrans/architecture-governance-engine` is frozen. Local clones: remote `ariadne` is the new repository, `origin` the old one; push with `git push ariadne HEAD:<branch>`, never to `origin`.

## Current Position

- **Milestone:** 2 — Rule engine v2 [active since 2026-11-04]
- **Phase 2.1 — Semantic dependency analysis:** complete. PR #25 merged and released as 0.2.0. PR #29, the changelog fix, merged 2026-11-05. PR #30, branch `chore/complete-phase-2.1`, marks the phase complete in ROADMAP.md and MILESTONE.md and ticks the 2.1 plan's checkboxes; it waits for the owner's merge.
- **Phase 2.2 — Rule schema v2:** pending; design and plan written and approved by the owner on 2026-11-05.
  - Design: `docs/superpowers/specs/2026-11-05-phase-2.2-rule-schema-v2-design.md`
  - Plan: `docs/superpowers/plans/2026-11-05-phase-2.2-rule-schema-v2.md`, 14 tasks
  - Both are in PR #33, branch `docs/phase-2.2-rule-schema-v2`, waiting for the owner's merge.
  - Execution method chosen by the owner: **subagent-driven**.

Phase 2.2 decisions by the owner, 2026-11-05:
- Layers keep `allowedDependencies` with an `id` and `because`; a separate `rules:` list holds `forbid` and `allowOnly`.
- A child pack may only retarget an inherited layer's selectors, add rules and add exceptions to unlocked rules.
- `.architecture/rules.yaml` becomes a standalone pack selected by assembly.
- `componentRoles` stay, strictly typed; `codeFeatureRules` is removed until 2.3 brings it back typed.
- Selectors in 2.2 are namespace, assembly and type name; base type, interface and attribute come in 2.3.
- The strict loader is a hand-written node walk over YamlDotNet's representation model.
- Two packs defining the same layer fail to compose; only role-only add-ons compose.
- A type in two layers is a `LayerOverlap` error, with no precedence rule.
- Module isolation is out of scope, tracked in #32.
- `Linter`, CLI `lint` and MCP `lint_source` are deleted.

## Open Decisions

- An own YAML library: a separate, unscheduled project outside Ariadne (agreed 2026-10-31).
- From the roadmap design, none blocking: whether M3 ships a Roslyn analyzer; code generation's role after M4; Mnemosyne.NET integration.
- #32: module-isolation rule type for modular monoliths needs a design and a phase slot.

## Blockers

- PR #33 must merge before Phase 2.2 implementation starts.
- **actionlint false positives:** actionlint 1.7.12 reports two errors on `actions/create-github-app-token@v3` `client-id`, an actionlint metadata bug tracked as rhysd/actionlint#648 and #669. Expect exactly these two until it ships a fix.

## Follow-ups

- Open Renovate and Dependabot PRs on the repository, among them majors for YamlDotNet 18, xunit runner 4, MSBuild 18 and ModelContextProtocol 2. Phase 2.2 pins YamlDotNet at 16.3.0; review #20 only after 2.2 lands.
- ZeroAlloc-Net/ZeroAlloc.TestHelpers#106: once fixed, switch the layer-rule allocation test back to `AllocationGate`.
- Phase 2.1 perf baseline: `docs/perf/phase-2.1-baseline.md`; Task 13 of the 2.2 plan re-measures it.

## Recommended Next Step

Confirm PR #33 is merged; if not, wait for the owner. Then run project-orchestration `start-next-phase`. Phase 2.2 has a design and a plan, so it routes to `list-phase-assumptions` and then `superpowers:subagent-driven-development` on branch `feat/phase-2.2-rule-schema-v2`, in its own worktree from `ariadne/main`. Tasks 4 and 6 run on Opus; the rest on Sonnet. Tick each plan step's checkbox as tasks finish, so `complete-phase` can close the phase.

Commit rules: conventional commits; no `Co-Authored-By` trailer or session link; no nested parentheses in commit bodies. Shared VM: one build or test at a time, no local AOT publish.
