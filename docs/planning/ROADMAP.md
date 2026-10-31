# Project Roadmap

Design: `docs/superpowers/specs/2026-10-30-roadmap-design.md`

## Milestone 1: Ariadne.NET identity and family shape [status: active]
**Goal:** The engine is renamed to Ariadne.NET, restructured into the family's package shape and published for the first time.
**Started:** 2026-10-30
**Definition of Done:**
- [ ] No `ArchGov` names remain in code, packages, CLI or docs, apart from historical plan documents
- [ ] `Ariadne.NET.Abstractions` and `Ariadne.NET` build as separate packages
- [ ] Public engine APIs return ZeroAlloc Results instead of throwing for expected failures
- [ ] GitHub repo exists with CI, codecov and docs site, and a first version is released by release-please

### Phase 1.1: Rename to Ariadne.NET [status: active]
**Goal:** Rename solution, namespaces, packages, CLI and docs from ArchGov to Ariadne.NET.
**Surface:** Refactor
**HelpWanted:** no
**Design:** `docs/superpowers/specs/2026-10-31-phase-1.1-rename-design.md`
**Plan:** `docs/superpowers/plans/2026-10-31-phase-1.1-rename.md`

### Phase 1.2: Package split [status: pending]
**Goal:** Split into `Ariadne.NET.Abstractions` (schema and loader), `Ariadne.NET` (engine) and the surfaces.
**Surface:** Refactor
**HelpWanted:** no
**Plan:** _to be written_

### Phase 1.3: Family conventions [status: pending]
**Goal:** Adopt ZeroAlloc Results instead of exceptions, get the ZeroAlloc analyzers clean, and support AOT where the dependencies allow it.
**Surface:** Refactor
**HelpWanted:** no
**Plan:** _to be written_

### Phase 1.4: Publish [status: pending]
**Goal:** Create the GitHub repo, CI with codecov, the first release-please release, the docs site and the Family README section.
**Surface:** Infra
**HelpWanted:** no
**Plan:** _to be written_

## Milestone 2: Rule engine v2 [status: pending]
**Goal:** Packs can express and enforce everything the Daedalus rules need, checked against the real type graph.
**Definition of Done:**
- [ ] Every rule type in the design's Daedalus appendix is expressible in YAML and enforced
- [ ] Dependency checks use the semantic type graph
- [ ] A rule that matches no types fails
- [ ] Built-in packs are migrated to schema v2 and a JSON Schema is published

### Phase 2.1: Semantic dependency analysis [status: pending]
**Goal:** Analyse a Roslyn compilation instead of scanning `using` directives.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 2.2: Rule schema v2 [status: pending]
**Goal:** A versioned schema with `forbid` and `allowOnly` at layer, namespace and type level, exception lists, and a `because` reason on every rule.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 2.3: Enforce placement and code-feature rules [status: pending]
**Goal:** Make YAML-defined placement and code-feature rules actually run.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 2.4: Banned packages and name collisions [status: pending]
**Goal:** Add rule types for banned packages and cross-layer name collisions.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 2.5: Vacuity detection [status: pending]
**Goal:** A rule that matches no types fails instead of passing silently.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 2.6: Pack reference and JSON Schema [status: pending]
**Goal:** Document every rule type and publish a JSON Schema for editor validation.
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

## Milestone 6: Ecosystem [status: pending]
**Goal:** Ariadne packs are the one architecture rule format across the user's tools.
**Definition of Done:**
- [ ] roslyn-codelens evaluates Ariadne packs
- [ ] Packs can be shared through NuGet as well as git
- [ ] The docs site has family cross-links and a pack catalogue

### Phase 6.1: roslyn-codelens reads Ariadne packs [status: pending]
**Goal:** `check_architecture` evaluates Ariadne packs, with Ariadne supplying the Abstractions package.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 6.2: NuGet pack sharing [status: pending]
**Goal:** NuGet as a pack-sharing source beside git sync.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 6.3: Docs site complete [status: pending]
**Goal:** Family cross-links and a pack catalogue.
**Surface:** Docs
**HelpWanted:** no
**Plan:** _to be written_
