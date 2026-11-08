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

## Milestone 2: Rule engine v2 [status: active]
**Goal:** Packs can express and enforce everything the Daedalus rules need, checked against the real type graph.
**Started:** 2026-11-04
**Design:** `docs/superpowers/specs/2026-11-04-milestone-2-design.md`
**Definition of Done:**
- [ ] Every rule type in the design's Daedalus appendix is expressible in a v2 pack and enforced, shown by a fixture solution with a clean and a broken variant per rule type
- [ ] Dependency checks use the semantic type graph; `SourceValidator` is deleted and CLI `validate` and MCP `validate_source` use the new engine
- [ ] `Ariadne.NET.Workspaces` loads solutions through MSBuildWorkspace and is published beside the other two packages
- [ ] A rule or layer selector that matches no type fails, unless the rule sets `allowEmpty: true`
- [ ] Packs can forbid namespaces, types and members per layer for security reasons, such as SQL, crypto or HTTP access outside Infrastructure
- [ ] The v2 loader rejects unknown keys and rules without a `because`
- [ ] Built-in packs and `.architecture/rules.yaml` are migrated to schema v2 and a JSON Schema is published
- [ ] The milestone's changes ship to nuget.org in a release-please release

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

### Phase 2.4: Banned packages, forbidden APIs and name collisions [status: pending]
**Goal:** Rule types for banned packages over the resolved package graph, member-level forbidden APIs per layer with the security worked example, and the same type name kept in two layers.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 2.5: Vacuity detection [status: pending]
**Goal:** A rule or layer selector that matches no type fails with its own violation kind, unless the rule opts out with `allowEmpty: true`.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 2.6: Pack reference and JSON Schema [status: pending]
**Goal:** Document every v2 rule type on the docs site, publish a JSON Schema for editor validation, and delete the drifted `docs/how-it-works/` copy.
**Surface:** Docs
**HelpWanted:** no
**Plan:** _to be written_

## Milestone 3: Fail the build [status: pending]
**Goal:** Any .NET solution can enforce its Ariadne packs in tests and CI.
**Definition of Done:**
- [ ] One test call checks all packs against a solution
- [ ] CLI and testing package emit JSON and SARIF
- [ ] `ariadne` is installable as a dotnet tool
- [ ] A GitHub Actions example fails a build on a violation

### Phase 3.1: Ariadne.NET.Testing [status: pending]
**Goal:** One call in a test checks all packs against the solution.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 3.2: JSON and SARIF reports [status: pending]
**Note:** Use ZeroAlloc.Serialisation for the JSON and SARIF writers.
**Goal:** Machine-readable reports from the CLI and the testing package.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 3.3: dotnet tool [status: pending]
**Goal:** Ship the CLI as the `ariadne` dotnet tool.
**Surface:** Infra
**HelpWanted:** no
**Plan:** _to be written_

### Phase 3.4: CI examples [status: pending]
**Goal:** GitHub Actions examples that fail a build on a violation.
**Surface:** Docs
**HelpWanted:** no
**Plan:** _to be written_

## Milestone 4: Daedalus parity [status: pending]
**Goal:** Ariadne proves it can replace Daedalus's ArchUnitNET architecture tests.
**Definition of Done:**
- [ ] A Daedalus pack covers all 34 unit-level architecture tests
- [ ] The parity harness shows Ariadne and ArchUnitNET agree on clean and deliberately broken variants
- [ ] A migration guide from ArchUnitNET exists

### Phase 4.1: Daedalus pack [status: pending]
**Goal:** Write the pack covering Daedalus's structural and project-specific rules.
**Surface:** Mixed
**HelpWanted:** no
**Plan:** _to be written_

### Phase 4.2: Parity harness [status: pending]
**Goal:** Run Ariadne and ArchUnitNET on a Daedalus checkout, including deliberately broken variants, and compare verdicts.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 4.3: Close vocabulary gaps [status: pending]
**Goal:** Fix every gap the parity harness finds.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 4.4: Migration guide [status: pending]
**Goal:** Document moving from ArchUnitNET to Ariadne.
**Surface:** Docs
**HelpWanted:** no
**Plan:** _to be written_

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
