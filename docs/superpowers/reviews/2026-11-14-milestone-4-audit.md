# Milestone 4 audit — ArchUnitNET parity

**Date:** 2026-11-14
**Verdict:** FAIL, one gap and one deviation that needs the owner's decision
**Audited at:** `main` abab816, release 0.9.1 on nuget.org; Daedalus `main` 423bba73

## Criteria

The criteria are the Definition of Done in `docs/planning/MILESTONE.md`.

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | All planned phases complete | PASS | Phases 4.1 to 4.6 are `[complete]` in ROADMAP.md and MILESTONE.md, and every plan has all task boxes ticked. 4.1 to 4.5 merged as #99, #102, #104 (with #114), #106 and #107 and were released in 0.8.0. 4.6 ran in Daedalus as #456, merged as 423bba73, and the guide update #131 merged here as edcd716. |
| 2 | All tests passing | PASS | CI on `main` edcd716, the last code commit, is green on ubuntu and windows with the .NET 10 and 11 SDKs, including `aot-smoke`, `pack-validate` and `fail-the-build`. abab816 changes only `docs/planning/STATE.md`. Not run locally: on this machine three `SolutionLoaderTests` for unrestored projects fail on `main` too, as STATE.md records. |
| 3 | The parity catalogue lists every type-level and slice kind of ArchUnitNET 0.13.2, each with its equivalent or marked deferred or out of scope, with the reason | PASS | `docs-site/docs/archunitnet-parity.md`, pinned at 0.13.2; `CatalogueConsistencyTests` checks it against `ParityCases`, `KnownGaps` and `Divergences`. Deferred rows link #94, #95 or #96. |
| 4 | Clean and broken variants for every in-scope entry, both tools agree except for catalogued `Divergences`, `KnownGaps` holds no type-level or slice entry | **FAIL** | `KnownGaps` holds `p-have-any-attributes:not`, a type-level case: ArchUnitNET's `DoNotHaveAnyAttributes` predicate has no pack form, because rule exceptions do not take `attributes`. The catalogue marks it `gap`, `unplanned`, tracked in #97, which says it needs a decision. The other parts hold: 22 `Divergences` entries, each in the catalogue. |
| 5 | Packs express naming pattern, base type, attribute, sealed, abstract, static, nested and slice rules; the pack JSON Schema and its conformance tests cover them | PASS | Selector traits and `is`/`isNot` from 4.2, `slices` from 4.3, all in `docs-site/static/schema/pack.schema.json` with `PackSchema` cases. |
| 6 | `modular-monolith` forbids one module from depending on another's internals, a fixture proves it, #32 closed | PASS | `MM-MODULES-INDEPENDENT` in `src/Ariadne.NET/BuiltinPacks/modular-monolith.yaml`, fixture `tests/fixtures/modular`, #32 closed. |
| 7 | A migration guide maps each ArchUnitNET construct to pack YAML with Daedalus as the worked example | PASS | `docs-site/docs/migrating-from-archunitnet.md`, checked by `MigrationGuideSnippetTests` and `MigrationGuideTests`; the worked example shows the pack Daedalus ships since #131. |
| 8 | Daedalus `main` references no ArchUnitNET package, CI checks an Ariadne pack through `Ariadne.NET.Testing`, the only C# architecture tests left are `NuGetPackageRootPathTests` and the DI integration test | **Deviation** | No ArchUnitNET reference on Daedalus 423bba73; `Ariadne.NET.Testing` 0.9.1 runs `AriadneArchitectureTests` in `Daedalus.Tests.Unit`, which CI runs through `dotnet test Daedalus.sln`. But a third C# file is left: `RetainedArchitectureTests`, two reflection checks that keep `DAE-RAGNET-ONLY-IN-AGENTS` load-bearing, because its `Rag.**` target never appears in the solution and so cannot use `requireTarget`. The guide documents it. The owner merged #456 with it, but the criterion's wording was never amended. |
| 9 | Member-level rule kinds are filed as one issue | PASS | #94 "ArchUnitNET member-level rule kinds". |

## Pre-push reviews

No `docs/pre-push-review-*.md` report exists. Warning, not a failure: each phase had per-task reviews and a final branch review in its subagent-driven run, and CodeRabbit reviewed the phase PRs and Daedalus #456.

## Release check

Conventions say `Milestone completion tags a release: no`; release-please owns tagging, so the criterion is skipped.

## Gaps

1. `KnownGaps` entry `p-have-any-attributes:not`, #97.
2. Criterion 8's wording against `RetainedArchitectureTests` in Daedalus.

## Open follow-ups, not blocking

#132 build warnings from `Microsoft.NET.StringTools` 18.12.2; #94, #95, #96 deferred kinds; red Renovate PRs #28 and #20.
