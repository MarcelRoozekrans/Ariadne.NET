# Milestone 4 Design — ArchUnitNET parity

> Filled in from `templates/milestone-design.template.md` by `new-milestone`. Inputs: `docs/superpowers/specs/2026-10-30-roadmap-design.md` (Milestone 4 and its appendix), the Milestone 4 block in `docs/planning/ROADMAP.md`, `docs-site/docs/how-it-works/03-rule-schema.md` and `rule-types.md` on `main` at `78c2130`, Daedalus.NET `main` at `1dd470b7` (2026-11-10), issue #32, and four answers from the owner on 2026-11-11.

**Date:** 2026-11-11
**Milestone:** 4
**Stage:** milestone (one milestone, multiple phases)
**Status:** approved 2026-11-11

## Goal

Anyone who writes architecture rules in ArchUnitNET can write the same rules in Ariadne and get the same verdicts. A catalogue on the docs site lists each ArchUnitNET type-level rule kind together with its Ariadne equivalent. A harness in this repo runs both tools side by side on the same fixture variants and fails CI if they disagree. Ariadne gains the type-level conditions and slice rules it lacks today, and a migration guide shows how to move over. The milestone ends with Daedalus, the first real consumer, replacing its ArchUnitNET tests with an Ariadne pack. Ariadne stays a standalone product throughout: nothing in it depends on Daedalus.

## Why the milestone changed

The roadmap named this milestone "Daedalus parity": prove the pack format against Daedalus's 34 ArchUnitNET tests before other tools depend on it. On 2026-11-11 the owner asked why Ariadne needs Daedalus at all, and wants Ariadne to be usable on its own. Proving the format against one codebase's habits also leaves the general claim untested, which is "what I would write in ArchUnitNET, I can write in Ariadne". The milestone therefore proves parity against ArchUnitNET's vocabulary, using fixtures in this repo. Daedalus moves from being the test bed to being the first consumer, and its switch-over is the last phase.

## Where things stand today

Read from `main` on 2026-11-11:

- **What Ariadne rules can already express:** `forbid` and `allowOnly` (dependency rules, with exceptions), `require` with `layer`, `kind`, `accessibility` and `implements`, `bannedPackages` (transitive, glob patterns), `uniqueNames`, and `members` on the `to` target. Selectors match `layers`, `namespaces`, `assemblies`, `types`, `baseTypes`, `interfaces`, `attributes` and `members`. Vacuity detection fails a rule that selects nothing.
- **What is missing compared with ArchUnitNET:**
  - a naming pattern, base type or attribute as a *condition* (only `implements` exists today);
  - `sealed`, `abstract`, `static` and `nested`, as either a selector or a condition;
  - slices, that is cycles between namespace groups and independence between them;
  - member-level rules such as `CallAny`, return types and immutability.
- **Daedalus has drifted since the 2026-10-30 review:**
  - It still has 34 unit tests in `CleanArchitectureTests`, but `EfCore` became `ZeroAllocOrm` and `Controllers_ShouldResideIn_ApiLayer` became `Endpoints_ShouldResideIn_ApiLayer`. The latter rule is `HaveNameEndingWith("Endpoints")` → `ResideInAssembly(Api)`, which `require { for: { types: ["**.*Endpoints"] }, layer: Api }` already covers.
  - `NoEntityFrameworkTests` is new: no project may resolve EF Core, even transitively. `bannedPackages` already covers it.
  - `NuGetPackageRootPathTests` is new: it scans MSBuild files as text.
- **Issue #32 overlaps with slices.** It asks for module isolation in modular monoliths, and its proposed shape, a capture segment such as `{module}.Core.**`, is the same idea as ArchUnitNET's `Slices().Matching("Shop.(*).Core")`.
- **The pack-schema conformance tests have a `KnownGaps` list** for what a schema cannot check. The parity harness reuses that pattern.

## Decisions

Taken with the owner on 2026-11-11:

- **Parity with ArchUnitNET, not with Daedalus.** Parity is proved in this repo, on fixtures in this repo. Ariadne takes no dependency on Daedalus.
- **Scope: type-level rule kinds plus slices.** M4 covers:
  - naming pattern, base type and attribute as conditions;
  - `sealed`, `abstract`, `static` and `nested`;
  - slice cycles and slice independence.

  Member-level rules (`CallAny`, return types, immutability) are catalogued as deferred and filed as one issue. Custom C# predicates are out of scope, following the roadmap's "rules stay YAML only" principle.
- **The harness runs both tools side by side (approach A).** Each test runs the ArchUnitNET rule and the Ariadne rule on the same fixture variant, and asserts that both give the same verdict and flag the same offending types. ArchUnitNET is a test-only dependency. Gaps that are not closed yet sit in an explicit `KnownGaps` list, and a phase closes a gap by deleting its entry.
- **Divergences are allowed if they are documented.** ArchUnitNET reads compiled IL and Ariadne reads source, so on some constructs they can never agree: inlined `const` values, and compiler-generated types from lambdas and async methods. These go in a separate `Divergences` list, next to `KnownGaps`. Each entry has a fixture that shows the difference, the reason for it, and which tool's answer a developer would expect, and the catalogue shows it. At the end of M4, `KnownGaps` must be empty for in-scope kinds. `Divergences` need not be.
- **The last phase switches Daedalus over.** That phase works in the Daedalus repo:
  - add an `.architecture/` pack and one `Ariadne.NET.Testing` test;
  - delete the 34 `CleanArchitectureTests` and `NoEntityFrameworkTests`;
  - remove the `TngTech.ArchUnitNET.xUnit` package.

## Approaches considered for the proof

- **A — Side by side in one test run (chosen).** Both tools run on the same built fixture, and their verdicts and offending types are compared. This makes the strongest claim, and it can never go stale. It costs a test-only ArchUnitNET dependency and a fixture that has to build before the tests run.
- **B — Recorded expectations.** Run ArchUnitNET once, record its verdicts as JSON, and compare Ariadne against that file. CI then has no ArchUnitNET, but the file goes stale silently when either tool changes.
- **C — Ariadne-only fixtures.** Use hand-written expected verdicts. This is the simplest, but it only shows that Ariadne is correct by our own reading, not that it agrees with ArchUnitNET.

## Definition of Done

- [ ] All planned phases complete
- [ ] All tests passing
- [ ] The docs site has a parity catalogue that lists every type-level and slice rule kind of the pinned ArchUnitNET version. Each entry has its Ariadne equivalent, or is marked deferred (member level) or out of scope (custom predicates), with the reason.
- [ ] Every in-scope catalogue entry has a clean and a broken fixture variant. CI runs ArchUnitNET and Ariadne on both, and they agree on the verdict and the offending types, except where a `Divergences` entry with a fixture and a reason covers the difference. `KnownGaps` holds no type-level or slice entry. Every `Divergences` entry appears in the catalogue.
- [ ] Packs can express type-level conditions (naming pattern, base type, attribute, `sealed`, `abstract`, `static`, `nested`) and slice rules (no cycles, no dependency between slices). The pack JSON Schema and its conformance tests cover them.
- [ ] The built-in `modular-monolith` pack forbids one module's types from depending on another module's internals, a fixture proves it, and #32 is closed.
- [ ] A migration guide on the docs site maps each ArchUnitNET construct to pack YAML and translates Daedalus's rules as a worked example.
- [ ] Daedalus `main` references no ArchUnitNET package. Its CI checks an Ariadne pack through `Ariadne.NET.Testing`, and the only architecture tests left in C# are `NuGetPackageRootPathTests` and the DI integration test.
- [ ] Member-level rule kinds are filed as one issue.

## Phases

1. **Phase 4.1: Parity catalogue and harness** — `Surface: Backend`
   - **Goal:** Catalogue ArchUnitNET's rule kinds and build the side-by-side harness on a fixture solution, green for the kinds Ariadne covers today and with every other kind listed in `KnownGaps`.
2. **Phase 4.2: Type-level conditions** — `Surface: Backend`
   - **Goal:** Add naming pattern, base type, attribute, `sealed`, `abstract`, `static` and `nested` as conditions and, where the catalogue needs them, as selectors, and remove their `KnownGaps` entries.
3. **Phase 4.3: Slices** — `Surface: Backend`
   - **Goal:** Add namespace capture segments and slice rules for "no cycles" and "no dependency between slices", use them in the `modular-monolith` pack, and close #32.
4. **Phase 4.4: Target vacuity** — `Surface: Backend`
   - **Goal:** A `forbid`, `allowOnly` or `uniqueNames` rule can set `requireTarget: true` and then fails as `VacuousRule` when its target matches no type the solution declares or references. Inserted on 2026-11-11 after the CodeRabbit review of PR #84, so the guide can map Daedalus's load-bearing facts to it.
5. **Phase 4.5: Migration guide** — `Surface: Docs`
   - **Goal:** Document moving from ArchUnitNET to Ariadne, construct by construct, with Daedalus's rules translated as the worked example.
6. **Phase 4.6: Daedalus switch-over** — `Surface: Mixed`
   - **Goal:** In the Daedalus repo, replace the ArchUnitNET tests with an Ariadne pack checked through `Ariadne.NET.Testing`, and remove the ArchUnitNET package.

## Dependencies on Prior Milestones

- **Depends on M2:** rule schema v2, the semantic analysis, placement rules, banned packages, vacuity detection, and the pack JSON Schema with its conformance tests.
- **Depends on M3:** `Ariadne.NET.Testing`, which runs one call over every pack under `.architecture/`. The harness uses it for the Ariadne side, and Daedalus uses it in 4.6.
- **4.6 depends on a release.** Daedalus consumes Ariadne from nuget.org, so 4.6 needs a published version that contains whatever its pack uses. Daedalus's current rules need nothing new from 4.2 or 4.3. They might still use 4.2's conditions if those read more clearly, and the phase decides that. Releases are cut by merging the release-please PR, which is the owner's call.

## External Constraints

- **4.6 runs in another repository.** Phase 4.6 changes Daedalus.NET, so it runs in a session opened on that repository. The phase completes here once its PR has merged there.
- **Daedalus keeps moving.** Its rules changed twice in three weeks. Phase 4.6 reads Daedalus `main` when it starts, not the state recorded in this design.
- **ArchUnitNET's version is pinned.** The catalogue and the harness are written against one pinned version. Moving to a newer version is a deliberate change that updates both.

## Risk Areas

| Risk | Impact | Mitigation |
|---|---|---|
| ArchUnitNET reads compiled IL and Ariadne reads source through Roslyn, so they see different dependencies: compiler-generated types, inlined `const` values, `nameof`, attribute arguments | The harness reports disagreements that are not real gaps | Compare only types declared in source. Every remaining difference becomes a `Divergences` entry with a fixture and a reason, and it is shown in the catalogue. A difference is never filtered silently. |
| Slices need a new schema concept (capture segments), the same design question as #32 | 4.3 grows into a schema redesign | 4.3's phase design settles the capture syntax first. If it does not fit one phase, split 4.3 rather than widen it. |
| New conditions and slice rules change the pack schema | `pack.schema.json` and its conformance tests drift from the loader | Every phase that adds schema updates the JSON Schema and its conformance tests in the same PR, as 2.6 established |
| Building the fixture solution makes the test run slower | CI and local runs get slower | One fixture solution with one project per variant, built once per test run, as the existing fixtures are |
| Daedalus is not ready for 4.6 when M4 reaches it | M4 cannot close | 4.6 is the last phase and has no dependants. If Daedalus is blocked, the owner decides whether M4 waits or closes without it. |
| Disagreement over what ArchUnitNET flags for an edge case | The catalogue overstates parity | The harness is the arbiter: a catalogue entry is "covered" only when its fixture pair agrees in CI |

## Calls made while writing this up

Settled by Claude, not discussed; correct any of them.

- **Phase order.** The harness comes first, so every later phase is measured against it. The guide comes before the switch-over, so that 4.6 can follow it, but the guide is not final until 4.6 has done the switch-over once.
- **The catalogue is a docs-site page.** The harness tests cite the page's entry ids, so the page and the tests can be checked against each other.
- **The fixture lives in `tests/fixtures/archunit-parity/`.** It has one project per rule kind and variant, so a broken variant cannot leak into a clean one.
- **The ArchUnitNET package is the core `TngTech.ArchUnitNET`,** not the xUnit adapter. The harness calls `Evaluate` and compares results itself.
- **Two Daedalus tests stay in Daedalus.** `NuGetPackageRootPathTests` is a text scan of MSBuild files, not a type rule. The integration test needs the running DI container.
- **The milestone is renamed in ROADMAP.md to "ArchUnitNET parity".** The roadmap design's appendix stays as the historical input, and this design supersedes it for M4.
- **The existing rule kinds are measured, not assumed.** 4.1 runs the harness on the kinds Ariadne already has. A disagreement goes to `Divergences` when it comes from IL versus source, and is fixed in 4.1 when it is a small bug in the current analysis. Anything larger becomes a `KnownGaps` entry and gets its own phase, through `add-phase`.
- **Combinators.** Ariadne's selectors already express ArchUnitNET's `And` (several lists in one selector) and its `Or` within one predicate (several values in one list). `Not` maps to `forbid` and to exceptions. A general boolean expression, such as `Or` across different predicates or nested `Not`, is catalogued as out of scope, so 4.2 adds fields and not an expression language.
- **ArchUnitNET is pinned at 0.13.2,** the version Daedalus already runs on .NET 10. That shows Mono.Cecil reads .NET 10 assemblies. 4.1 confirms it on the fixture as its first task.
- **"Offending types" means `ArchitectureViolation.SourceType`,** which every violation already carries. The harness groups Ariadne's violations by `SourceType` and compares the resulting set with ArchUnitNET's failing types. No change to the violation model or to the output contract of `Ariadne.NET.Testing`.
- **`modular-monolith` keeps its convention.** Its modules are `<Module>.Public`, `.Core` and `.Infrastructure`, as the built-in pack declares today. 4.3 adds the slice rule on that convention. Making the pack configurable is not part of M4.
- **4.6 starts on the latest release.** If its pack needs something that is merged but not released, the phase stops and asks the owner to merge the release-please PR. It never builds against an unreleased package.
- **Member-level kinds are filed as one issue** when 4.1's catalogue lists them, not at the end of the milestone.
- **Issue #36 stays out of scope.** Daedalus's pack is a standalone pack and does not amend inherited layers. If 4.6 finds it needs #36, the phase stops and asks.

## Open Questions

None that block `new-milestone`. Per-phase questions, such as the capture syntax for slices and whether conditions also become selectors, belong to each phase's design.
