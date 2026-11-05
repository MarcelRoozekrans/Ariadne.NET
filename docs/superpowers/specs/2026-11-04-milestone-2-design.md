# Milestone 2 Design — Rule engine v2

> Filled in from `templates/milestone-design.template.md` by `new-milestone`. Inputs: `docs/superpowers/specs/2026-10-30-roadmap-design.md` (Milestone 2 and the Daedalus appendix), the Milestone 2 block in `docs/planning/ROADMAP.md`, a read of the current engine on `main` at `f2a4b48`, and three answers from the user on 2026-11-04.

**Date:** 2026-11-04
**Milestone:** 2
**Stage:** milestone (one milestone, multiple phases)

## Goal

A v2 pack can state every rule in the roadmap design's Daedalus appendix, and the engine enforces each one against the real type graph of a solution instead of its `using` lines. A rule says which types it covers through namespace and assembly patterns, carries a `because` that every violation repeats, and fails when it covers nothing. Packs can also wall off sensitive APIs per layer, such as SQL, crypto or HTTP access outside Infrastructure. The CLI and the MCP server run the same engine, so there is one answer to "is this allowed here?". At the end of the milestone Ariadne is ready for Milestone 3 to put it in CI, and for Milestone 4 to write the real Daedalus pack against it.

## Where the engine stands today

Read from `main` on 2026-11-04:

- `Validation/SourceValidator.cs` globs `*.cs` under a directory, including `bin/` and `obj/`, and parses each file on its own. It has no compilation and no semantic model. A file's layer is the last namespace segment that equals a layer name, and each `using` is mapped to a layer the same way. Fully qualified names, global usings, aliases and type references are never seen. Issue #1 is one symptom.
- Layers are matched by a single namespace segment, so the dotted layers of the built-in `modular-monolith` pack, such as `Module.Public`, never match anything.
- `codeFeatureRules` are loaded, merged and returned by MCP `get_rules`, but nothing evaluates them. `AppliesTo` is an untyped dictionary. `CODE-001` and `CODE-002` look enforced only because `SourceValidator` hard-codes the same two checks from role fields.
- The schema has no placement rule, no `forbid` or `allowOnly`, no exceptions and no `because`. `Version` is a free string that nothing reads, and the YAML has no schema version.
- The deserializer runs with `IgnoreUnmatchedProperties`, so a mistyped key is dropped without a word.
- `RuleEngine/DependencyValidator.cs` takes layer names, not code, and nothing outside `RuleEngine/` calls it.
- The 10 built-in packs and this repo's `.architecture/rules.yaml` are all written against this v1 shape.

## Decisions

Taken by the user on 2026-11-04:

- **Selectors are namespace globs and assembly names.** A layer, and any other rule target, lists namespace globs such as `*.Domain.**`, assembly names such as `Daedalus.Agents`, or both. External code such as Thalos, EF Core or Rag.NET is matched the same way, so one mechanism covers in-solution layers, projects and third-party dependencies. This also fixes the dotted layers of `modular-monolith`.
- **Solutions load through MSBuildWorkspace.** The core engine takes a Roslyn `Compilation`, or a set of them, so a Thalos agent can pass its own without a project on disk. The CLI, the MCP server and Milestone 3's testing package open a `.sln`, `.slnx` or `.csproj` with MSBuildWorkspace and MSBuildLocator. That gives an exact semantic graph with file and line locations and the projects' package references. It needs the .NET SDK and a restored solution, and it is slower, in seconds per solution.
- **The syntax-only validator is replaced, not kept.** CLI `validate` and MCP `validate_source` move to the semantic engine in this milestone, and `SourceValidator` with its hard-coded checks is deleted. There is no second, faster engine that could disagree with the first. Issue #1 closes with this change.

Carried over from the roadmap design:

- **Rules stay YAML only.** No C# rule classes. A check that no general rule type can express stays an ordinary test in the consuming repo.
- **Schema v2 is a clean break.** There is no v1 loader and no dual-version support. The 10 built-in packs and `.architecture/rules.yaml` are migrated, and every engine part that reads a pack moves to the v2 model.

## Calls made while writing this up

Settled by Claude, not discussed. Correct any of them in the PR.

- **MSBuildWorkspace lives in a new package, `Ariadne.NET.Workspaces`.** The core `Ariadne.NET` package takes a `Compilation` and does not reference MSBuild, so an in-process caller such as a Thalos agent does not pull in MSBuild and MSBuildLocator. The CLI and the MCP server reference `Ariadne.NET.Workspaces`. This adds a third NuGet package under the reserved `Ariadne.NET` prefix, so CI's pack-validate and publish steps grow by one.
- **Unknown YAML keys are an error.** The v2 loader rejects any key the schema does not define and names it with its line. A pack that loads is a pack whose every line was read.
- **The schema version is a required top-level field,** for example `schema: 2`. The pack's own `version` stays the pack author's release number and is unrelated.
- **A `because` is required on every rule.** A pack without one does not load. Every violation carries the rule id and its `because`, so a person or an agent reading the violation sees the reason without opening the pack.
- **Type-level selectors are by name pattern, base type, implemented interface and attribute,** in addition to namespace and assembly. Placement by role needs the first three; Daedalus's `OnlySubagentRunExecutor_DependsOn_ISubagentRunner` needs a type named exactly. The full selector grammar is phase 2.2's to design.
- **Dependencies are type references in the semantic graph:** base types, implemented interfaces, member signatures, attributes, generic arguments and types used in method bodies. A `using` that is never used is not a dependency.
- **Banned packages are checked against each project's resolved package graph, transitive packages included,** read from the restored project. A banned package that arrives transitively is a violation, since it is on the build's path either way.
- **Forbidden APIs cover namespaces, types and members.** Phase 2.2 gives `forbid` its namespace and type targets, enough for the ten Daedalus external-namespace rules. Phase 2.4 adds member targets such as one method on a type, and the security examples. A dedicated built-in security pack is not planned: layer names differ between packs, so the pack reference carries a worked example instead.
- **Vacuity fails by default, with a per-rule opt-out.** A rule, or a layer selector, that matches no type fails with its own violation kind. A rule can set `allowEmpty: true`. The built-in packs set it on their role-placement rules only, because a generic pack such as `ddd-tactical` names roles a given solution may not have. Layer selectors in built-in packs have no opt-out: a solution that extends `clean-architecture` without a Domain layer should hear about it.
- **Naming conventions stay with code generation.** `NamingConventions` is not validated in this milestone.
- **The `docs/how-it-works/` copy, which has drifted from `docs-site/docs/how-it-works/`, is deleted in phase 2.6** in favour of the docs-site pages.
- **The checks are open-world.** A layer's allowed dependencies constrain only references to types that some layer of the pack covers. A reference to BCL or third-party code that no selector names is not a violation. External code is governed from the target side: `forbid` says a layer must not use it, and `allowOnly` on an external target says only these layers or types may use it, which is the shape of Daedalus's `OnlyAgentsProject_DependsOn_RagNet`. A type that no layer covers is not a violation either.
- **A type covered by two layers is an error** reported for that type, not a first-match pick. Overlapping globs are a pack mistake and should be visible.
- **Generated code is not a rule subject by default.** Types declared only in generated sources, recognised by Roslyn's generated-code rules (`<auto-generated>` headers, `.g.cs` and similar names, `[GeneratedCode]`), are not checked as the source of a dependency. They still count as dependency targets. A pack can opt in. Without this, a DI source generator's registration class would break every layer rule. `bin/` and `obj/` are not scanned at all, because input comes from the project's compilation, not a directory glob.
- **A workspace that does not load fully fails the run.** If MSBuildWorkspace reports a project it could not load or a compilation with errors that leave types unresolved, the result is an Ariadne error that names the project, not a list of violations. A half-loaded solution would make vacuity detection report false empties.
- **Multi-targeting projects are checked once per target framework, and identical violations are reported once,** keyed on rule, type and location. A violation that exists in only one target framework names it.
- **The violation model is designed in phase 2.1 for the whole milestone.** It carries rule id, violation kind, `because`, the source type, the target type or package, and a file and line when there is one. On the v1 packs of phase 2.1 the `because` is empty. Phase 2.2 fills it rather than changing the shape. The CLI's JSON and SARIF output is Milestone 3's work; until then the public result types may still change, which a 0.x release allows.
- **CLI `validate` and MCP `validate_source` take a `.sln`, `.slnx` or `.csproj` path,** or a directory holding exactly one of them, in place of today's source directory. Checking a code snippet that is not part of a project is not supported; it belongs with the agent tools in Milestone 5.
- **Exceptions are per rule.** A rule lists the types or namespaces it does not apply to, and each exception carries its own `because`. Daedalus's `OnlySubagentRunExecutor_DependsOn_ISubagentRunner`, with its two named exceptions, is the test case.
- **`extends` keeps its current meaning:** a child pack adds rules, and adds exceptions to inherited rules. A `locked` rule cannot be changed or relaxed by a child. The merge details of the new fields are phase 2.2's.
- **No performance threshold is set yet.** Phase 2.1 records the time and allocations of checking this repo's own solution as a baseline that later phases must not regress by more than they explain.
- **Evidence for the Daedalus appendix is a fixture solution in this repo.** It mirrors the shape of Daedalus closely enough to hold one rule of each appendix type, each with a clean and a deliberately broken variant. The real Daedalus pack and the comparison with ArchUnitNET stay in Milestone 4.

## Definition of Done

- [ ] All planned phases complete
- [ ] All tests passing
- [ ] Every rule type in the roadmap design's Daedalus appendix is expressible in a v2 pack and enforced: the fixture solution holds one rule of each type, the clean variant passes and the broken variant fails with that rule's id and `because`
- [ ] Dependency checks run on the semantic type graph of a `Compilation`; `SourceValidator` is deleted and CLI `validate` and MCP `validate_source` use the new engine; issue #1 is closed
- [ ] `Ariadne.NET.Workspaces` loads a `.sln`, `.slnx` or `.csproj` through MSBuildWorkspace and is packed and published by CI beside the other two packages
- [ ] A rule or layer selector that matches no type fails, unless the rule sets `allowEmpty: true`
- [ ] A pack can forbid namespaces, types and members per layer, and the pack reference shows SQL, crypto and HTTP access confined to Infrastructure as a worked example
- [ ] The v2 loader rejects unknown keys and rules without a `because`
- [ ] All 10 built-in packs and `.architecture/rules.yaml` are on schema v2, and this repo passes its own pack in CI
- [ ] A JSON Schema for v2 is in the repo and on the docs site, and the docs-site pack reference documents every rule type
- [ ] The milestone's changes ship to nuget.org in a release-please release

## Phases

1. **Phase 2.1: Semantic dependency analysis** — `Surface: Backend`
   - **Goal:** Build a type dependency graph from a Roslyn `Compilation`, add `Ariadne.NET.Workspaces` to load solutions through MSBuildWorkspace, and replace `SourceValidator` behind CLI `validate` and MCP `validate_source`, still on the v1 layer rules. Its hard-coded role checks for a missing interface and accessibility move onto the graph unchanged, so nothing stops being checked before phase 2.3 replaces them.
2. **Phase 2.2: Rule schema v2** — `Surface: Backend`
   - **Goal:** A versioned, strict schema with namespace and assembly selectors, `forbid` and `allowOnly` at layer, namespace and type level, exception lists and a required `because`, with the built-in packs, this repo's pack and every engine part that reads a pack migrated to it.
3. **Phase 2.3: Enforce placement and code-feature rules** — `Surface: Backend`
   - **Goal:** Placement-by-role rules and typed code-feature rules run on the type graph, and `CODE-001` and `CODE-002` are enforced from the pack instead of hard-coded checks.
4. **Phase 2.4: Banned packages, forbidden APIs and name collisions** — `Surface: Backend`
   - **Goal:** Rule types for banned packages over the resolved package graph, member-level forbidden APIs per layer, and the same type name kept in two layers.
5. **Phase 2.5: Vacuity detection** — `Surface: Backend`
   - **Goal:** A rule or layer selector that matches no type fails with its own violation kind, unless the rule opts out with `allowEmpty: true`.
6. **Phase 2.6: Pack reference and JSON Schema** — `Surface: Docs`
   - **Goal:** Document every v2 rule type on the docs site with examples, including the security worked example, publish a JSON Schema for editor validation, and delete the drifted `docs/how-it-works/` copy.

Phase 2.1 comes first because every rule type in 2.2 to 2.5 is evaluated against its graph. It keeps the v1 layer rules on purpose, so the new engine is proven on today's packs before the schema changes under it. Phases 2.3, 2.4 and 2.5 each need 2.2's selectors and can be done in any order after it; the order above follows the Daedalus appendix's counts. Phase 2.6 documents what 2.2 to 2.5 built.

## Dependencies on Prior Milestones

- **Milestone 1:** the `Ariadne.NET.Abstractions` and `Ariadne.NET` split, ZeroAlloc Results on public APIs, CI with pack-validate and Trusted Publishing, and release-please. Phase 2.1 adds a third package to that pipeline.
- **The roadmap design's Daedalus appendix** is the vocabulary checklist. No Daedalus checkout is needed in this milestone.
- **External:** MSBuildLocator needs a .NET SDK on the machine that runs the check. GitHub's runners have one; an in-process caller that passes its own `Compilation` does not need one.

## External Constraints

- Schema v2 changes public types in `Ariadne.NET.Abstractions`. With `bump-minor-pre-major` set, release-please releases it as 0.2.0, which is correct for a breaking change before 1.0. Commits that break the API are marked with `!` or a `BREAKING CHANGE:` footer so the changelog says so.
- Nothing outside this repo depends on the pack format yet. Milestone 4 is when consumers start to, so the schema can still change freely until then.

## Risk Areas

| Risk | Impact | Mitigation |
|---|---|---|
| MSBuildWorkspace needs a restore, and its MSBuild assemblies conflict with the SDK's if they are copied to the output | Tests that open fixture solutions are slow or fail to load, and CLI users see load errors | Follow MSBuildLocator's rule that `Microsoft.Build.*` stays out of the output; restore fixtures once per test run; report workspace diagnostics as Ariadne errors instead of silently checking a half-loaded solution |
| The type-graph walk is slow or allocates heavily on a large solution | Long CI runs, and slow agent loops in Milestone 5 | Measure on this repo's solution in phase 2.1; the roadmap note suggests ZeroAlloc.TestHelpers allocation budgets for the hot path |
| Vacuity detection fails ordinary solutions that extend a broad built-in pack | Users turn rules off or stop extending built-ins | `allowEmpty: true` on the built-in packs' role-placement rules; layer selectors still fail, which is the useful signal |
| The schema grows a field per Daedalus test instead of general rule types | A schema that fits one solution and nobody else | Each rule type in 2.2 to 2.4 must also make sense for at least one built-in pack, not only Daedalus |
| A breaking v2 lands without the `!` marker | release-please releases it as a patch | Phase plans name the commits that break the public API; review checks the marker |
| Strict YAML loading or the v2 model adds code that is not AOT-safe to `Ariadne.NET.Abstractions` | The AotSmoke test breaks | Keep the AotSmoke test in every phase's verification |

## Open Questions

None that block the milestone. Left to the phase brainstorms:

- The exact selector and glob grammar, including how `*` and `**` treat namespace segments: phase 2.2.
- Whether `Ariadne.NET.Workspaces` caches a loaded workspace between MCP calls: phase 2.1, and Milestone 5 for the agent tools.
- Whether the JSON Schema is generated from the C# model or written by hand: phase 2.6.
