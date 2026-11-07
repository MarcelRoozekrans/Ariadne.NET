# Session State

**Last session:** 2026-11-07 (phase 5.6 implemented)
**Repository:** `MarcelRoozekrans/Ariadne.NET`, public since 2026-11-03. The old private repository `MarcelRoozekrans/architecture-governance-engine` is frozen. Local clones: remote `ariadne` is the new repository, `origin` the old one; push with `git push ariadne HEAD:<branch>`, never to `origin`.

## Current Position

- **Milestone:** 2 — Rule engine v2 [active since 2026-11-04]
- **Phase 2.1 — Semantic dependency analysis:** complete, released as 0.2.0.
- **Phase 2.2 — Rule schema v2:** [status: complete 2026-11-05]. PR #35 merged as f1d8a14; 0.3.0 released via release-please PR #38, which lists all four breaking changes. Completion merged as #39.
  - Design: `docs/superpowers/specs/2026-11-05-phase-2.2-rule-schema-v2-design.md`
  - Plan: `docs/superpowers/plans/2026-11-05-phase-2.2-rule-schema-v2.md`, all boxes ticked.
  - Perf: the check on the repo pack is about 21% slower and allocates about 32% more than the 2.1 baseline; see `docs/perf/phase-2.1-baseline.md`.
- **Phase 2.3 — Enforce placement and code-feature rules:** [status: complete]. Merged as #44 on 2026-11-06; completion merged as #45 and 0.4.0 released via #41. Design `docs/superpowers/specs/2026-11-06-phase-2.3-placement-and-code-feature-rules-design.md`, plan `docs/superpowers/plans/2026-11-06-phase-2.3-placement-and-code-feature-rules.md`, all boxes ticked. Owner decisions: one `require` rule with roles kept as engine-internal shorthand; `CODE-001` keeps its solution-wide meaning; no placement rules in the built-in packs, only in this repo's own pack.
- **Phase 5.6 — Agent hooks:** [status: complete 2026-11-07]. Implemented on branch `feat/phase-5.6-hooks`, pushed to remote `ariadne`; the PR is open. Plan `docs/superpowers/plans/2026-11-06-phase-5.6-agent-hooks.md`, all boxes ticked; design `docs/superpowers/specs/2026-11-06-agent-guidance-hooks-design.md`. Timings in `docs/perf/phase-5.6-hooks.md`: `session-start` and `pre-tool-use` about 0.1 s, `stop` with one edit about 13 s on this solution. Owner decisions, approved 2026-11-07, beyond the design: pack attachments move to 5.7; `pre-tool-use` records edits for `stop`; the new-file check reads the namespace from the written content. Follow-ups:
  - Pack attachments move to phase 5.7, together with `init --from <url>`.
  - Per-edit validation through `PostToolUse`: the measurement says no; it would need a resident process.
  - Make `Ariadne.NET.Hooks` packable in 5.7.
  - Native AOT for the `ariadne hook` entry point only if a later measurement shows it slow; process start is about 0.1 s today.
  - The `ariadne` command must be on `PATH`, which needs the dotnet tool of phase 3.3. The Milestone 5 hooks item stays unticked until then.
  - The flaky Hooks test was `EditedFilesTests.Concurrent_records_all_land`: an append race in `EditedFiles`, because .NET seeks to the end once at open, so parallel hook processes wrote at the same offset and lost or split records. Fixed in the final review wave: one small file per edit in a per-user, per-session folder, and `stop` removes only the records it read.
  - Owner decision pending: `stop_hook_active` currently clears the edit list. Not clearing it would check the edits made while fixing in the next turn.
  - A custom hook `timeout` on an existing Ariadne entry in `.claude/settings.json` is reset by every `init` run.
  - SIGTERM during `stop` can exit 143, which breaks the fail-open rule.
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

- Open Renovate and Dependabot PRs, among them majors for YamlDotNet 18 (#20), MSBuild 18 (#28) and ModelContextProtocol 2 (#14). Phase 2.2 pins YamlDotNet at 16.3.0; phase 2.2 has landed, so #20 can be reviewed.
- ZeroAlloc-Net/ZeroAlloc.TestHelpers#106: once fixed, switch the allocation tests from `GC.GetAllocatedBytesForCurrentThread` back to `AllocationGate`.
- The old `docs/how-it-works/*` copy still describes removed features; Phase 2.6 deletes it.
- Deferred minors from the reviews: the Daedalus fixture never exercises the Agents layer's `allowedDependencies`; `Owned<T>` and the reader have no further known gaps.
- **Parked, approved 2026-11-06:** phase 5.7 Guidance delivery, designed in `docs/superpowers/specs/2026-11-06-agent-guidance-hooks-design.md` (stage B). Owner decisions: one tool with a separate `Ariadne.NET.Guidance` package; Ariadne owns structure and delivery while AI.Sentinel owns tool-call enforcement. It takes over the pack attachments that 5.6 left out.

## Recommended Next Step

Phase 5.6 is implemented and reviewed. Merge the 5.6 PR from `feat/phase-5.6-hooks`, then run `complete-phase` for 5.6. The PR is squash merged, with a `BEGIN_COMMIT_OVERRIDE` block in its body, one line per commit and no breaking changes. Check afterwards that release-please lists the new features. The next phase in Milestone 2 is 2.4: run `start-next-phase`, which routes to brainstorming because there is no design spec. Check the release-please PR afterwards that the commits were counted.

Commit rules: conventional commits; no `Co-Authored-By` trailer or session link; no nested parentheses in commit bodies; keep every footer line under 100 characters, because commitlint rejects longer ones; breaking commits carry `!` and a `BREAKING CHANGE` footer. Shared VM: one build or test at a time, no local AOT publish.
