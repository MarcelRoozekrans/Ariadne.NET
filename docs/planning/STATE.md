# Session State

**Last session:** 2026-11-05
**Repository:** `MarcelRoozekrans/Ariadne.NET`, public since 2026-11-03. The old private repository `MarcelRoozekrans/architecture-governance-engine` is frozen. Local clones: remote `ariadne` is the new repository, `origin` the old one; push with `git push ariadne HEAD:<branch>`, never to `origin`.

## Current Position

- **Milestone:** 2 — Rule engine v2 [active since 2026-11-04]
- **Phase 2.1 — Semantic dependency analysis:** complete, released as 0.2.0.
- **Phase 2.2 — Rule schema v2:** [status: active]. All 14 plan tasks are implemented and reviewed, and a whole-branch review plus one fix wave is done. PR #35, branch `feat/phase-2.2-rule-schema-v2`, is open, CI is green on both OS legs, commitlint, pack-validate and aot-smoke, and it waits for the owner's merge.
  - Design: `docs/superpowers/specs/2026-11-05-phase-2.2-rule-schema-v2-design.md`
  - Plan: `docs/superpowers/plans/2026-11-05-phase-2.2-rule-schema-v2.md`
  - One plan checkbox is still open on purpose, so `complete-phase` refuses until it is settled: Task 14 step 4, the `feat!` commits showing as breaking changes in the release-please PR, which can only be checked after the merge. Task 12 step 3 (actionlint) is ticked: it is not installed on the build VM, but GitHub accepted the workflow and ran the new step green.
  - Verified before the PR: Release build 0 warnings; tests 751, 45, 38 and 65 pass; `validate src/Ariadne.NET.slnx` passes on the repo's own pack; docs-site build passes.
  - Perf: the check on the repo pack is about 21% slower and allocates about 32% more than the 2.1 baseline; an empty pack allocates the same, so the cost is in the type graph. See `docs/perf/phase-2.1-baseline.md`.
- Issues filed from this phase: #36 (child packs: amending `allowedDependencies`, locked rules sidestepped by retargeting, empty retargets) and #37 (child-layer error wording, duplicate roles, pack file size limit). #32 stays the module-isolation item.

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

- PR #35 needs the owner's merge.
- **actionlint false positives:** actionlint 1.7.12 reports two errors on `actions/create-github-app-token@v3` `client-id`, an actionlint metadata bug tracked as rhysd/actionlint#648 and #669. Expect exactly these two until it ships a fix.

## Follow-ups

- Open Renovate and Dependabot PRs, among them majors for YamlDotNet 18, xunit runner 4, MSBuild 18 and ModelContextProtocol 2. Phase 2.2 pins YamlDotNet at 16.3.0; review #20 only after 2.2 lands.
- ZeroAlloc-Net/ZeroAlloc.TestHelpers#106: once fixed, switch the allocation tests from `GC.GetAllocatedBytesForCurrentThread` back to `AllocationGate`.
- The old `docs/how-it-works/*` copy still describes removed features; Phase 2.6 deletes it.
- Deferred minors from the reviews: the Daedalus fixture never exercises the Agents layer's `allowedDependencies`; `Owned<T>` and the reader have no further known gaps.

## Recommended Next Step

After the owner merges PR #35: check that the release-please PR lists the `feat!` commits as breaking changes, then tick Task 14 step 4 in the plan and run project-orchestration `complete-phase` for 2.2 on a branch from `ariadne/main`. Then run `start-next-phase` for Phase 2.3 (no design yet, so it routes to brainstorming). Until the merge, the only work is answering review comments on PR #35 in branch `feat/phase-2.2-rule-schema-v2`.

Commit rules: conventional commits; no `Co-Authored-By` trailer or session link; no nested parentheses in commit bodies; keep every footer line under 100 characters, because commitlint rejects longer ones; breaking commits carry `!` and a `BREAKING CHANGE` footer. Shared VM: one build or test at a time, no local AOT publish.
