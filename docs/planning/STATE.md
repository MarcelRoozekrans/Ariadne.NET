# Session State

**Last session:** 2026-11-09 (phase 2.6 built on branch `phase-2-6-pack-reference`, no PR yet)
**Repository:** `MarcelRoozekrans/Ariadne.NET`, public since 2026-11-03. The old private repository `MarcelRoozekrans/architecture-governance-engine` is frozen. Local clones: remote `ariadne` is the new repository, `origin` the old one; push with `git push ariadne HEAD:<branch>`, never to `origin`.

## Current Position

- **Milestone:** 2 — Rule engine v2 [active since 2026-11-04]
- **Phase 2.1 — Semantic dependency analysis:** complete, released as 0.2.0.
- **Phase 2.2 — Rule schema v2:** [status: complete 2026-11-05]. PR #35 merged as f1d8a14; 0.3.0 released via release-please PR #38, which lists all four breaking changes. Completion merged as #39.
  - Design: `docs/superpowers/specs/2026-11-05-phase-2.2-rule-schema-v2-design.md`
  - Plan: `docs/superpowers/plans/2026-11-05-phase-2.2-rule-schema-v2.md`, all boxes ticked.
  - Perf: the check on the repo pack is about 21% slower and allocates about 32% more than the 2.1 baseline; see `docs/perf/phase-2.1-baseline.md`.
- **Phase 2.3 — Enforce placement and code-feature rules:** [status: complete]. Merged as #44 on 2026-11-06; completion merged as #45 and 0.4.0 released via #41. Design `docs/superpowers/specs/2026-11-06-phase-2.3-placement-and-code-feature-rules-design.md`, plan `docs/superpowers/plans/2026-11-06-phase-2.3-placement-and-code-feature-rules.md`, all boxes ticked. Owner decisions: one `require` rule with roles kept as engine-internal shorthand; `CODE-001` keeps its solution-wide meaning; no placement rules in the built-in packs, only in this repo's own pack.
- **Phase 5.6 — Agent hooks:** [status: complete 2026-11-08]. Merged as #47 (e609dcf) on 2026-11-08 from `feat/phase-5.6-hooks`; CI was green. Release-please PR #48 (0.4.1) lists the hook features and fixes and is not merged. Plan `docs/superpowers/plans/2026-11-06-phase-5.6-agent-hooks.md`, all boxes ticked; design `docs/superpowers/specs/2026-11-06-agent-guidance-hooks-design.md`. Timings in `docs/perf/phase-5.6-hooks.md`: `session-start` and `pre-tool-use` about 0.1 s, `stop` with one edit about 13 s on this solution. Owner decisions, approved 2026-11-07, beyond the design: pack attachments move to 5.7; `pre-tool-use` records edits for `stop`; the new-file check reads the namespace from the written content. Follow-ups:
  - Pack attachments move to phase 5.7, together with `init --from <url>`.
  - Per-edit validation through `PostToolUse`: the measurement says no; it would need a resident process.
  - Make `Ariadne.NET.Hooks` packable in 5.7.
  - Native AOT for the `ariadne hook` entry point only if a later measurement shows it slow; process start is about 0.1 s today.
  - The `ariadne` command must be on `PATH`, which needs the dotnet tool of phase 3.3. The Milestone 5 hooks item stays unticked until then.
  - The flaky Hooks test was `EditedFilesTests.Concurrent_records_all_land`: an append race in `EditedFiles`, because .NET seeks to the end once at open, so parallel hook processes wrote at the same offset and lost or split records. Fixed in the final review wave: one small file per edit in a per-user, per-session folder, and `stop` removes only the records it read.
  - Owner decision 2026-11-08: stop clearing the edit list on `stop_hook_active`, so edits made while fixing are checked in the next turn. Not yet implemented.
  - Owner decision 2026-11-08: `init` preserves a custom hook `timeout` on an existing Ariadne entry in `.claude/settings.json` and only fills a missing one. Not yet implemented.
  - Owner decision 2026-11-08: fix SIGTERM during `stop` exiting 143 by exiting 0, so the fail-open rule holds. Not yet implemented.
- **Phase 2.4 — Banned packages, forbidden APIs and name collisions:** [status: complete 2026-11-08], merged as #51 on 2026-11-08 from `feat/phase-2.4-banned-packages-forbidden-apis-name-collisions`; CI was green after one fix to the Hooks stop test count. Released as 0.5.0 through #52. Design `docs/superpowers/specs/2026-11-08-phase-2.4-banned-packages-forbidden-apis-name-collisions-design.md`, plan `docs/superpowers/plans/2026-11-08-phase-2.4-banned-packages-forbidden-apis-name-collisions.md`, all boxes ticked. Adds `bannedPackages` (package graph from `project.assets.json`, transitive and through project references, unknown data is an error for a covering rule), `members` on the `to` selector of `forbid` and `allowOnly` (override and implement matching, initializers, implicit base constructor call, indexers by metadata name) and `uniqueNames`. Perf: `docs/perf/phase-2.4-baseline.md`; the walker and validator are within 0.1% of the 2.3 head, the extra cost per type is the larger solution. Stated limits are in the design and on the docs-site rule-schema page.
  - Done: the PR carried a `BEGIN_COMMIT_OVERRIDE` block and release-please PR #52 shows the `RuleDefinitionValidator` constructor break under BREAKING CHANGES.
  - Issue #50: the new `InternalsVisibleTo Ariadne.NET.Workspaces` couples the two packages' versions. Owner decision 2026-11-08: replace it with a public coverage check API. Not yet implemented.
  - Leftover branches deleted on `ariadne` 2026-11-08 with the owner's go-ahead: `docs/phase-2.4-design`, `docs/agent-hooks-design`, `feat/phase-5.6-agent-hooks` and the merged 2.4 feature branch. The local 2.4 feature branch remains because another worktree has it checked out.
  - Not covered by a CLI or MCP level test: the stale-assets error (shared `SolutionValidator` is tested).
- **Phase 2.5 — Vacuity detection:** [status: complete 2026-11-08]. Merged as #53 (98ed64f) on 2026-11-08 from `feat/phase-2.5-vacuity-detection`; CI was green after one fix to the packed-consumer check. Release 0.5.0 was released by the owner (#52). Release-please PR #54 (0.6.0) lists the `feat!` and the three features and is not merged. Design `docs/superpowers/specs/2026-11-08-phase-2.5-vacuity-detection-design.md`, plan `docs/superpowers/plans/2026-11-08-phase-2.5-vacuity-detection.md`, all boxes ticked except the PR-time note in Task 6. A rule, component role or layer that selects no type fails with `VacuousRule` (11) or `VacuousLayer` (12), unless a rule or role sets `allowEmpty: true`; subject side only; built-in packs set `allowEmpty` on every role. The default change is breaking: commit 35ae809 is `feat!` with two BREAKING CHANGE footers. The PR body carried a `BEGIN_COMMIT_OVERRIDE` block and #54 shows the four entries. Perf: `docs/perf/phase-2.5-baseline.md`, about +2.3% allocations from the pass. Deferred minors: the `VacuousRule` message cell in `03-rule-schema.md` is awkwardly split and the column header says "Message starts with"; `forbid` has no exact-message test; the layer anchor test reads `Namespaces[0]` only.
- **Phase 2.6 — Pack reference and JSON Schema:** [status: complete 2026-11-09], on branch `phase-2-6-pack-reference` (pushed to `ariadne`); no PR opened, the owner has not asked. Design `docs/superpowers/specs/2026-11-09-phase-2.6-pack-reference-and-json-schema-design.md`, plan `docs/superpowers/plans/2026-11-09-phase-2.6-pack-reference-and-json-schema.md`, all boxes ticked. Adds `docs-site/static/schema/pack.schema.json` (hand-written, draft 2020-12), conformance tests in `tests/Ariadne.NET.Tests/PackSchema` (shipped packs, an invalid corpus, `KnownGaps` for cross-references and `ShapeGaps` for what JSON Schema cannot express), the `rule-types` and `editor-support` docs pages, and deletes `docs/how-it-works/`. Owner decisions 2026-11-09: hand-written schema plus conformance tests; pack reference only. Follow-ups: issue #57 (`docs/features.md` is an old copy); the docs build only warns on broken links (`onBrokenLinks: 'warn'`), switching to `throw` is site-wide; the test YAML converter ignores non-scalar keys and later documents. The earlier guess that other docs-site pages are stale was checked and not found.
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

## Scheduling (owner, 2026-11-09)

- Issue #50 (public coverage check API instead of `InternalsVisibleTo`): its own fix PR, before further 2.6 build work. Not yet implemented.
- The three 5.6 follow-ups (`stop_hook_active` keeps the edit list, `init` preserves a custom hook `timeout`, SIGTERM during `stop` exits 0): one hooks fix PR. Not yet implemented.

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
- Deferred minors from the reviews: the Daedalus fixture never exercises the Agents layer's `allowedDependencies`; `Owned<T>` and the reader have no further known gaps.
- **Parked, approved 2026-11-06:** phase 5.7 Guidance delivery, designed in `docs/superpowers/specs/2026-11-06-agent-guidance-hooks-design.md` (stage B). Owner decisions: one tool with a separate `Ariadne.NET.Guidance` package; Ariadne owns structure and delivery while AI.Sentinel owns tool-call enforcement. It takes over the pack attachments that 5.6 left out.

## Recommended Next Step

Phases 2.1 to 2.6 are built; 2.6 waits for a PR on `phase-2-6-pack-reference`. That completes Milestone 2's phases, so the next step is `audit-milestone` once 2.6 is merged. Separately, per the owner on 2026-11-09: issue #50 gets its own fix PR, and the three 5.6 follow-ups get one hooks fix PR. Neither is started.

Commit rules: conventional commits; no `Co-Authored-By` trailer or session link; no nested parentheses in commit bodies; keep every footer line under 100 characters, because commitlint rejects longer ones; breaking commits carry `!` and a `BREAKING CHANGE` footer. Shared VM: one build or test at a time, no local AOT publish.
