# Project Roadmap

Design: `docs/superpowers/specs/2026-10-30-roadmap-design.md`

## Milestone 1: Ariadne.NET identity and family shape [status: complete]
**Goal:** The engine is renamed to Ariadne.NET, restructured into the family's package shape and published for the first time.
**Started:** 2026-10-30
**Completed:** 2026-11-04
**Definition of Done:**
- [x] No `ArchGov` names remain in code, packages, CLI or docs, apart from historical plan documents
- [x] `Ariadne.NET.Abstractions` and `Ariadne.NET` build as separate packages
- [x] Public engine APIs return ZeroAlloc Results instead of throwing for expected failures
- [x] GitHub repo exists with CI, codecov and docs site, and a first version is released by release-please
- [x] CI runs the security baseline: gitleaks, NuGetAudit on transitive packages, CodeQL, a CycloneDX SBOM per release, and a codecov coverage floor

### Phase 1.1: Rename to Ariadne.NET [status: complete]
**Goal:** Rename solution, namespaces, packages, CLI and docs from ArchGov to Ariadne.NET.
**Surface:** Refactor
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-10-31-phase-1.1-rename-design.md`
**Plan:** `docs/superpowers/plans/2026-10-31-phase-1.1-rename.md`
**Completed:** 2026-10-31

### Phase 1.2: Package split [status: complete]
**Goal:** Split into `Ariadne.NET.Abstractions` (schema and pack contracts), `Ariadne.NET` (engine) and the surfaces.
**Surface:** Refactor
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-10-31-phase-1.2-package-split-design.md`
**Plan:** `docs/superpowers/plans/2026-10-31-phase-1.2-package-split.md`
**Completed:** 2026-10-31

### Phase 1.3: Family conventions [status: complete]
**Goal:** Adopt ZeroAlloc Results instead of exceptions, get the ZeroAlloc analyzers clean, and support AOT where the dependencies allow it.
**Surface:** Refactor
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-10-31-phase-1.3-family-conventions-design.md`
**Plan:** `docs/superpowers/plans/2026-10-31-phase-1.3-family-conventions.md`
**Completed:** 2026-10-31

### Phase 1.4: Publish [status: complete]
**Goal:** Create the GitHub repo, CI with codecov and a coverage floor, the security baseline (gitleaks on every pull request after the one-time history scan, `NuGetAuditMode=all`, CodeQL, a CycloneDX SBOM attached to each release), the first release-please release, the docs site and the Family README section. Trivy waits until a container image ships.
**Surface:** Infra
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-10-31-phase-1.4-publish-design.md`
**Plan:** `docs/superpowers/plans/2026-11-03-phase-1.4-publish.md`
**Completed:** 2026-11-04

## Milestone 2: Rule engine v2 [status: complete]
**Goal:** Packs can express and enforce everything the Daedalus rules need, checked against the real type graph.
**Started:** 2026-11-04
**Completed:** 2026-11-10
**Design:** `docs/superpowers/specs/2026-11-04-milestone-2-design.md`
**Definition of Done:**
- [x] Every rule type in the design's Daedalus appendix is expressible in a v2 pack and enforced, shown by a fixture solution with a clean and a broken variant per rule type
- [x] Dependency checks use the semantic type graph; `SourceValidator` is deleted and CLI `validate` and MCP `validate_source` use the new engine
- [x] `Ariadne.NET.Workspaces` loads solutions through MSBuildWorkspace and is published beside the other two packages
- [x] A rule or layer selector that matches no type fails, unless the rule sets `allowEmpty: true` (subject selectors only: `from` and `for`)
- [x] Packs can forbid namespaces, types and members per layer for security reasons, such as SQL, crypto or HTTP access outside Infrastructure
- [x] The v2 loader rejects unknown keys and rules without a `because`
- [x] Built-in packs and `.architecture/rules.yaml` are migrated to schema v2 and a JSON Schema is published
- [x] The milestone's changes ship to nuget.org in a release-please release

### Phase 2.1: Semantic dependency analysis [status: complete]
**Note:** Consider ZeroAlloc.TestHelpers allocation budgets for the type-graph hot path; consolidate the two UnixNonRootFactAttribute copies (and UnixFact) into TestHelpers.
**Goal:** Build a type dependency graph from a Roslyn `Compilation`, add `Ariadne.NET.Workspaces` to load solutions through MSBuildWorkspace, and replace `SourceValidator` behind CLI `validate` and MCP `validate_source` on the v1 layer rules; closes #1.
**Surface:** Backend
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-04-phase-2.1-semantic-dependency-analysis-design.md`
**Plan:** `docs/superpowers/plans/2026-11-04-phase-2.1-semantic-dependency-analysis.md`
**Completed:** 2026-11-05

### Phase 2.2: Rule schema v2 [status: complete]
**Note:** Consider ZeroAlloc.Specification for rule matching; extend the ZeroAlloc.Validation rules from 1.3.
**Goal:** A versioned, strict schema with namespace and assembly selectors, `forbid` and `allowOnly` at layer, namespace and type level, per-rule exceptions and a required `because`, with the built-in packs, this repo's pack and every engine part that reads a pack migrated to it.
**Surface:** Backend
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-05-phase-2.2-rule-schema-v2-design.md`
**Plan:** `docs/superpowers/plans/2026-11-05-phase-2.2-rule-schema-v2.md`
**Completed:** 2026-11-05

### Phase 2.3: Enforce placement and code-feature rules [status: complete]
**Note:** Consider ZeroAlloc.Specification for placement and code-feature matching.
**Goal:** Placement-by-role rules and typed code-feature rules run on the type graph, and `CODE-001` and `CODE-002` are enforced from the pack instead of hard-coded checks.
**Surface:** Backend
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-06-phase-2.3-placement-and-code-feature-rules-design.md`
**Plan:** `docs/superpowers/plans/2026-11-06-phase-2.3-placement-and-code-feature-rules.md`
**Completed:** 2026-11-06

### Phase 2.4: Banned packages, forbidden APIs and name collisions [status: complete]
**Goal:** Rule types for banned packages over the resolved package graph, member-level forbidden APIs per layer with the security worked example, and the same type name kept in two layers.
**Surface:** Backend
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-08-phase-2.4-banned-packages-forbidden-apis-name-collisions-design.md`
**Plan:** `docs/superpowers/plans/2026-11-08-phase-2.4-banned-packages-forbidden-apis-name-collisions.md`
**Completed:** 2026-11-08

### Phase 2.5: Vacuity detection [status: complete]
**Goal:** A rule or layer selector that matches no type fails with its own violation kind, unless the rule opts out with `allowEmpty: true`.
**Surface:** Backend
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-08-phase-2.5-vacuity-detection-design.md`
**Plan:** `docs/superpowers/plans/2026-11-08-phase-2.5-vacuity-detection.md`
**Completed:** 2026-11-08

### Phase 2.6: Pack reference and JSON Schema [status: complete]
**Goal:** Document every v2 rule type on the docs site, publish a JSON Schema for editor validation, and delete the drifted `docs/how-it-works/` copy.
**Surface:** Docs
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-09-phase-2.6-pack-reference-and-json-schema-design.md`
**Plan:** `docs/superpowers/plans/2026-11-09-phase-2.6-pack-reference-and-json-schema.md`
**Completed:** 2026-11-09

## Milestone 3: Fail the build [status: complete]
**Goal:** Any .NET solution can enforce its Ariadne packs in tests and CI.
**Started:** 2026-11-10
**Completed:** 2026-11-11
**Audit:** `docs/superpowers/reviews/2026-11-11-milestone-3-audit.md` (PASS)
**Design:** `docs/superpowers/specs/2026-11-10-milestone-3-design.md`
**Definition of Done:**
- [x] All planned phases complete
- [x] All tests passing
- [x] One call in a test (plus the package's documented MSBuild reference line, if it cannot ship it) checks every pack under `.architecture/` against a solution and fails with the rule id, pack name and `because` of each violation; a fixture with two packs, one clean and one broken, proves it
- [x] `ariadne validate`, the stop hook and MCP validation check the same packs and name the pack per violation
- [x] CLI and testing package emit JSON and SARIF 2.1.0; a test validates the SARIF against the published schema
- [x] `ariadne` installs with `dotnet tool install` from the packed local feed in CI, and `publish-nuget` includes the package
- [x] A GitHub Actions example fails a build on a violation, and a job in this repo's CI proves the failing exit code on a broken fixture
- [x] The docs site documents the testing package, the report formats, the tool and the CI examples

### Phase 3.1: Ariadne.NET.Testing [status: complete]
**Goal:** One call in a test checks all packs against the solution.
**Surface:** Backend
**HelpWanted:** no
**Plan:** `docs/superpowers/plans/2026-11-10-phase-3.1-ariadne-net-testing.md`
**Completed:** 2026-11-11

### Phase 3.2: JSON and SARIF reports [status: complete]
**Note:** Use ZeroAlloc.Serialisation for the JSON and SARIF writers.
**Goal:** Machine-readable reports from the CLI and the testing package.
**Surface:** Backend
**HelpWanted:** no
**Plan:** `docs/superpowers/plans/2026-11-10-phase-3.2-json-and-sarif-reports.md`
**Completed:** 2026-11-11

### Phase 3.3: dotnet tool [status: complete]
**Goal:** Ship the CLI as the `ariadne` dotnet tool.
**Surface:** Infra
**HelpWanted:** no
**Plan:** `docs/superpowers/plans/2026-11-10-phase-3.3-dotnet-tool.md`
**Completed:** 2026-11-11

### Phase 3.4: CI examples [status: complete]
**Goal:** GitHub Actions examples that fail a build on a violation.
**Surface:** Docs
**HelpWanted:** no
**Plan:** `docs/superpowers/plans/2026-11-10-phase-3.4-ci-examples.md`
**Completed:** 2026-11-11

## Milestone 4: ArchUnitNET parity [status: active]
**Goal:** Anyone who writes architecture rules in ArchUnitNET can write the same rules in Ariadne and get the same verdicts, proved side by side in this repo; Daedalus is the first consumer to switch over.
**Started:** 2026-11-11
**Design:** `docs/superpowers/specs/2026-11-11-milestone-4-design.md`
**Definition of Done:**
- [ ] All planned phases complete
- [ ] All tests passing
- [ ] A parity catalogue on the docs site lists every type-level and slice rule kind of the pinned ArchUnitNET version with its Ariadne equivalent, or marks it deferred or out of scope
- [ ] The side-by-side harness agrees on every in-scope kind, apart from documented `Divergences`; `KnownGaps` holds no type-level or slice entry
- [ ] Packs express type-level conditions and slice rules, covered by the pack JSON Schema
- [ ] The `modular-monolith` pack enforces module boundaries and #32 is closed
- [ ] A migration guide from ArchUnitNET exists on the docs site
- [ ] Daedalus `main` references no ArchUnitNET package and checks an Ariadne pack in CI

### Phase 4.1: Parity catalogue and harness [status: pending]
**Goal:** Catalogue ArchUnitNET's rule kinds and build the side-by-side harness on a fixture solution, green for today's kinds with the rest in `KnownGaps`.
**Surface:** Backend
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-11-phase-4.1-parity-catalogue-and-harness-design.md` (approved 2026-11-11)
**Plan:** `docs/superpowers/plans/2026-11-11-phase-4.1-parity-catalogue-and-harness.md`

### Phase 4.2: Type-level conditions [status: pending]
**Goal:** Add naming pattern, base type, attribute, sealed, abstract, static and nested as conditions and, where needed, selectors.
**Surface:** Backend
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-11-phase-4.2-type-level-conditions-design.md` (approved 2026-11-11)
**Plan:** `docs/superpowers/plans/2026-11-11-phase-4.2-type-level-conditions.md`

### Phase 4.3: Slices [status: pending]
**Goal:** Add namespace capture segments and slice rules for no cycles and no dependency between slices, use them in `modular-monolith`, and close #32.
**Surface:** Backend
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-11-phase-4.3-slices-design.md` (approved 2026-11-11)
**Plan:** `docs/superpowers/plans/2026-11-11-phase-4.3-slices.md`

### Phase 4.4: Target vacuity [status: pending]
**Goal:** A `forbid`, `allowOnly` or `uniqueNames` rule can set `requireTarget: true` and then fails as `VacuousRule` when its target matches nothing the solution uses: for `forbid` and `allowOnly`, no type it declares or references, or no member edge for a `members` target; for `uniqueNames`, no type it declares.
**Surface:** Backend
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-11-phase-4.4-target-vacuity-design.md` (approved 2026-11-11)
**Plan:** `docs/superpowers/plans/2026-11-11-phase-4.4-target-vacuity.md`

### Phase 4.5: Migration guide [status: pending]
**Goal:** Document moving from ArchUnitNET to Ariadne, construct by construct, with Daedalus's rules as the worked example.
**Surface:** Docs
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-11-phase-4.5-migration-guide-design.md` (approved 2026-11-11)
**Plan:** `docs/superpowers/plans/2026-11-11-phase-4.5-migration-guide.md`

### Phase 4.6: Daedalus switch-over [status: pending]
**Goal:** In the Daedalus repo, replace the ArchUnitNET tests with an Ariadne pack checked through `Ariadne.NET.Testing`, and remove the ArchUnitNET package.
**Surface:** Mixed
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-11-phase-4.6-daedalus-switch-over-design.md` (approved 2026-11-11)
**Plan:** `docs/superpowers/plans/2026-11-11-phase-4.6-daedalus-switch-over.md`

## Milestone 5: Agent surfaces [status: pending]
**Goal:** Agents in the family runtime and in editors use Ariadne while writing code.
**Definition of Done:**
- [ ] The MCP server runs on rule engine v2 and installs as a dotnet tool or npx package
- [ ] An agent can ask where a component belongs and get violations with the rule's reason
- [ ] A Thalos agent can call Ariadne in-process
- [ ] A Claude Code agent gets the structural rules and violation feedback through `ariadne hook`
- [ ] A new project receives an organisation's standards and skills from a synced pack

### Phase 5.1: MCP server on engine v2 [status: pending]
**Goal:** Publish the MCP server as a dotnet tool and npx package, like roslyn-codelens.
**Surface:** Infra
**HelpWanted:** no
**Plan:** _to be written_

### Phase 5.2: Agent-facing tools [status: pending]
**Goal:** "Where does this go", and violations explained with the rule's reason.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 5.3: Thalos tool package [status: pending]
**Goal:** Let Thalos agents call Ariadne in-process.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 5.4: Ariadne.NET.Minos [status: pending]
**Goal:** Choose a component role from a free-text request, with calibrated confidence.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 5.5: Claude Code plugin [status: pending]
**Goal:** A superpowers-extensions plugin that teaches agents when to call Ariadne.
**Surface:** Docs
**HelpWanted:** no
**Plan:** _to be written_

### Phase 5.6: Agent hooks [status: complete 2026-11-08]
**Goal:** `ariadne hook` gives a Claude Code agent the resolved structural rules at session start and the validator's violations, with each rule's reason, at the end of a turn.
**Surface:** Backend
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-06-agent-guidance-hooks-design.md` (approved 2026-11-06, stage A)
**Note:** Starts after phase 2.3, which changes the loader the pack attachments extend. Runs next to AI.Sentinel's hook, which keeps tool-call enforcement. Merged as #47 (e609dcf) on 2026-11-08; timings in `docs/perf/phase-5.6-hooks.md`. The Milestone 5 item "A Claude Code agent gets the structural rules and violation feedback through `ariadne hook`" stays unticked: the hooks need `ariadne` on `PATH`, which needs the dotnet tool from phase 3.3.
**Plan:** `docs/superpowers/plans/2026-11-06-phase-5.6-agent-hooks.md` (written 2026-11-06, approved by the owner 2026-11-07; all tasks done, merged from `feat/phase-5.6-hooks` as #47)

### Phase 5.7: Guidance delivery [status: pending]
**Goal:** Packs carry standards, skills and opaque files, and `ariadne sync` delivers them to projects, so a new project connects once instead of copying rules.
**Surface:** Backend
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-11-06-agent-guidance-hooks-design.md` (approved 2026-11-06, stage B)
**Note:** Lives in a separate `Ariadne.NET.Guidance` package that the engine never references. Keep it distinct from phase 5.5, which ships skills about using Ariadne.
**Plan:** _to be written_

## Milestone 6: Ecosystem [status: pending]
**Goal:** Ariadne packs are the one architecture rule format across the user's tools.
**Definition of Done:**
- [ ] roslyn-codelens evaluates Ariadne packs
- [ ] Packs can be shared through NuGet as well as git
- [ ] The docs site has family cross-links and a pack catalogue
- [ ] A built-in pack maps its rules to ISO/IEC 25010 maintainability qualities

### Phase 6.1: roslyn-codelens reads Ariadne packs [status: pending]
**Goal:** `check_architecture` evaluates Ariadne packs, with Ariadne supplying the Abstractions package.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 6.2: NuGet pack sharing [status: pending]
**Note:** Use ZeroAlloc.Cache for the async NuGet pack source; deferred from 1.3 because the built-in registry is synchronous.
**Goal:** NuGet as a pack-sharing source beside git sync.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 6.3: Docs site complete [status: pending]
**Goal:** Family cross-links and a pack catalogue.
**Surface:** Docs
**HelpWanted:** no
**Plan:** _to be written_

### Phase 6.4: ISO/IEC 25010 maintainability pack [status: pending]
**Goal:** A built-in pack whose rules for modularity, coupling, cycles and layering each name the ISO/IEC 25010 maintainability sub-characteristic they protect, so a report reads against the standard. Structure only: complexity metrics stay with roslyn-codelens.
**Surface:** Mixed
**HelpWanted:** no
**Plan:** _to be written_
