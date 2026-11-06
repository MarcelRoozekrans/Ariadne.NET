# Session State

**Last session:** 2026-11-06 (phase 2.3 built and merged as PR 44)
**Repository:** `MarcelRoozekrans/Ariadne.NET`, public since 2026-11-03. The old private repository `MarcelRoozekrans/architecture-governance-engine` is frozen. Local clones: remote `ariadne` is the new repository, `origin` the old one; push with `git push ariadne HEAD:<branch>`, never to `origin`.

## Current Position

- **Milestone:** 2 — Rule engine v2 [active since 2026-11-04]
- **Phase 2.1 — Semantic dependency analysis:** complete, released as 0.2.0.
- **Phase 2.2 — Rule schema v2:** [status: complete 2026-11-05]. PR #35 merged as f1d8a14; release-please PR #38 (0.3.0) is open and lists all four breaking changes. Completion is committed on branch `chore/complete-phase-2.2`, pushed to `ariadne` and not yet in a PR.
  - Design: `docs/superpowers/specs/2026-11-05-phase-2.2-rule-schema-v2-design.md`
  - Plan: `docs/superpowers/plans/2026-11-05-phase-2.2-rule-schema-v2.md`, all boxes ticked.
  - Perf: the check on the repo pack is about 21% slower and allocates about 32% more than the 2.1 baseline; see `docs/perf/phase-2.1-baseline.md`.
- **Phase 2.3 — Enforce placement and code-feature rules:** [status: complete 2026-11-06]. PR #44 merged as 918551c; release-please PR #41 (0.4.0) lists both breaking changes. Completion is committed on branch `chore/complete-phase-2.3`, pushed to `ariadne`, not yet in a PR. Roles are not expanded into `require` rules (plan refinement); `rules` and `get_rules` list roles as roles. Deferred, out of scope: `ArchitectureValidator` does not call `RulePackReferenceCheck` itself, so a code-built pack passed straight to it skips those checks.
- **Phase 2.4 — Banned packages, forbidden APIs and name collisions:** [status: pending], no design yet.
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

- None for the roadmap. The 0.3.0 release notes were incomplete (release-please read only the first of four `BREAKING CHANGE` footers in the squash commit of #35); fixed 2026-11-05 by a `BEGIN_COMMIT_OVERRIDE` block in the body of PR #35 with one line per breaking change. Footers inside an override are ignored, so the breaking text must be the subject. Next time a PR carries several breaking changes, put that block in the PR body before merging.
- **actionlint false positives:** actionlint 1.7.12 reports two errors on `actions/create-github-app-token@v3` `client-id`, tracked as rhysd/actionlint#648 and #669.

## Follow-ups

- Open Renovate and Dependabot PRs, among them majors for YamlDotNet 18, xunit runner 4, MSBuild 18 and ModelContextProtocol 2. Phase 2.2 pins YamlDotNet at 16.3.0; review #20 only after 2.2 lands.
- ZeroAlloc-Net/ZeroAlloc.TestHelpers#106: once fixed, switch the allocation tests from `GC.GetAllocatedBytesForCurrentThread` back to `AllocationGate`.
- The old `docs/how-it-works/*` copy still describes removed features; Phase 2.6 deletes it.
- Deferred minors from the reviews: the Daedalus fixture never exercises the Agents layer's `allowedDependencies`; `Owned<T>` and the reader have no further known gaps.
- **Parked, approved 2026-11-06:** phases 5.6 Agent hooks and 5.7 Guidance delivery, designed in `docs/superpowers/specs/2026-11-06-agent-guidance-hooks-design.md` on branch `docs/agent-hooks-design`. Owner decisions: hooks are the delivery channel; Claude Code first; one tool with a separate `Ariadne.NET.Guidance` package; Ariadne owns structure and delivery while AI.Sentinel owns tool-call enforcement. Phase 2.3 merged as #44 (2026-11-06), so the plan can start. Next step: write the implementation plan for 5.6 (stage A only) with `superpowers:writing-plans` on this branch, saved as `docs/superpowers/plans/2026-11-06-phase-5.6-agent-hooks.md`. Re-read the loader (`PackYamlReader`), `ArchitectureValidator`, `ISolutionValidator`, `ValidateCommand` and `InitCommand` first, since 2.2 and 2.3 changed them. Open points for the plan are in the design doc under "Left to the builder"; run the builder check on the plan before asking the owner to review.

## Recommended Next Step

Merge the PR for `chore/complete-phase-2.3`, then merge release-please PR #41 when ready. Next phase is 2.4: run `start-next-phase`, which routes to brainstorming because there is no design spec. Check that the 0.4.0 notes keep both breaking changes after any further merge.

Commit rules: conventional commits; no `Co-Authored-By` trailer or session link; no nested parentheses in commit bodies; keep every footer line under 100 characters, because commitlint rejects longer ones; breaking commits carry `!` and a `BREAKING CHANGE` footer. Shared VM: one build or test at a time, no local AOT publish.
