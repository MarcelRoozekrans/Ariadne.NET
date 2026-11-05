# Session State

**Last session:** 2026-11-05 (resumed after the PR 35 merge)
**Repository:** `MarcelRoozekrans/Ariadne.NET`, public since 2026-11-03. The old private repository `MarcelRoozekrans/architecture-governance-engine` is frozen. Local clones: remote `ariadne` is the new repository, `origin` the old one; push with `git push ariadne HEAD:<branch>`, never to `origin`.

## Current Position

- **Milestone:** 2 — Rule engine v2 [active since 2026-11-04]
- **Phase 2.1 — Semantic dependency analysis:** complete, released as 0.2.0.
- **Phase 2.2 — Rule schema v2:** [status: complete 2026-11-05]. PR #35 merged as f1d8a14; release-please PR #38 (0.3.0) is open. Completion is committed on branch `chore/complete-phase-2.2`, pushed to `ariadne` and not yet in a PR.
  - Design: `docs/superpowers/specs/2026-11-05-phase-2.2-rule-schema-v2-design.md`
  - Plan: `docs/superpowers/plans/2026-11-05-phase-2.2-rule-schema-v2.md`, all boxes ticked.
  - Perf: the check on the repo pack is about 21% slower and allocates about 32% more than the 2.1 baseline; see `docs/perf/phase-2.1-baseline.md`.
- **Phase 2.3 — Enforce placement and code-feature rules:** [status: pending], no design yet. `codeFeatureRules` comes back typed here; selectors gain base type, interface and attribute.
- Issues filed from 2.2: #36 (child packs: amending `allowedDependencies`, locked rules sidestepped by retargeting, empty retargets) and #37 (child-layer error wording, duplicate roles, pack file size limit). #32 stays the module-isolation item.

Phase 2.2 decisions by the owner, 2026-11-05:
- Layers keep `allowedDependencies` with an `id` and `because`; a separate `rules:` list holds `forbid` and `allowOnly`.
- A child pack may only retarget an inherited layer's selectors, add rules and add exceptions to unlocked rules.
- `.architecture/rules.yaml` is a standalone pack selected by assembly.
- `componentRoles` stay, strictly typed; `codeFeatureRules` is removed until 2.3 brings it back typed.
- Selectors in 2.2 are namespace, assembly and type name; base type, interface and attribute come in 2.3.
- The strict loader is a hand-written node walk over YamlDotNet's representation model.
- Two packs defining the same layer fail to compose; only role-only add-ons compose.
- A type in two layers is a `LayerOverlap` error, with no precedence rule.
- Module isolation is out of scope, tracked in #32.
- `Linter`, CLI `lint` and MCP `lint_source` are deleted.

## Open Decisions

- #36: may a child pack amend an inherited layer's `allowedDependencies`; should retargeting respect `locked`; should an empty retarget be rejected? Matters for the M4 Daedalus pack.
- #32: module-isolation rule type for modular monoliths needs a design and a phase slot.
- An own YAML library: a separate, unscheduled project outside Ariadne (agreed 2026-10-31).
- From the roadmap design, none blocking: whether M3 ships a Roslyn analyzer; code generation's role after M4; Mnemosyne.NET integration.

## Blockers

- **Release notes for 0.3.0 are incomplete.** The squash commit of #35 has four `BREAKING CHANGE` footers, but release-please PR #38 lists only the first (the `IRulePackMerger.Merge` one). Missing: packs need `schema: 2`; CLI `lint` and MCP `lint_source` removed; the rules output and `get_layers` payload follow schema v2. Same failure as 0.2.0, fixed there afterwards by docs PR #29. Needs a decision before #38 is merged: edit the merged PR #35 body with a `BEGIN_COMMIT_OVERRIDE` block so release-please re-reads it, or fix the changelog after the release as for 0.2.0.
- **actionlint false positives:** actionlint 1.7.12 reports two errors on `actions/create-github-app-token@v3` `client-id`, tracked as rhysd/actionlint#648 and #669.

## Follow-ups

- Open Renovate and Dependabot PRs, among them majors for YamlDotNet 18, xunit runner 4, MSBuild 18 and ModelContextProtocol 2. Phase 2.2 pins YamlDotNet at 16.3.0; review #20 only after 2.2 lands.
- ZeroAlloc-Net/ZeroAlloc.TestHelpers#106: once fixed, switch the allocation tests from `GC.GetAllocatedBytesForCurrentThread` back to `AllocationGate`.
- The old `docs/how-it-works/*` copy still describes removed features; Phase 2.6 deletes it.
- Deferred minors from the reviews: the Daedalus fixture never exercises the Agents layer's `allowedDependencies`; `Owned<T>` and the reader have no further known gaps.

## Recommended Next Step

Branch `chore/complete-phase-2.2` (from `ariadne/main`, one commit, not yet in a PR). Resolve the 0.3.0 release-notes blocker with the owner, then run `start-next-phase`, which routes Phase 2.3 to `superpowers:brainstorming` because no design spec exists. Open a PR for the branch when asked; `main` requires one.

Commit rules: conventional commits; no `Co-Authored-By` trailer or session link; no nested parentheses in commit bodies; keep every footer line under 100 characters, because commitlint rejects longer ones; breaking commits carry `!` and a `BREAKING CHANGE` footer. Shared VM: one build or test at a time, no local AOT publish.
