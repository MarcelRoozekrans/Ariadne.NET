# Milestone 4 re-audit — ArchUnitNET parity

**Date:** 2026-11-15
**Verdict:** PASS
**Audited at:** `main` 659d938, release 0.9.2 on nuget.org; Daedalus.NET `main` 423bba73
**Previous audit:** `docs/superpowers/reviews/2026-11-14-milestone-4-audit.md`, FAIL on criterion 4 (`KnownGaps`) and a deviation on criterion 8

## Criteria

The criteria are the Definition of Done in `docs/planning/MILESTONE.md`, with criterion 8 as the owner amended it on 2026-11-14.

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | All planned phases complete | PASS | Phases 4.1 to 4.7 are complete in ROADMAP.md and MILESTONE.md, and every plan has all task boxes ticked; the only `- [ ]` left in each plan is the checkbox-syntax note in its header. 4.7, the gap phase from the previous audit, merged as #136 (df8f70d) and was released in 0.9.2 (603da3c, tag `v0.9.2`). |
| 2 | All tests passing | PASS | CI on `main` is green for 659d938, 603da3c and df8f70d, on ubuntu and windows with the .NET 10 and 11 SDKs, including `aot-smoke`, `pack-validate` and `fail-the-build`; `baseline-checks` is green too. 659d938 adds only `AGENTS.md`. Not re-run locally, for the reason the previous audit gives. |
| 3 | The parity catalogue lists every type-level and slice kind of ArchUnitNET 0.13.2, each with its equivalent or marked deferred or out of scope, with the reason | PASS | `docs-site/docs/archunitnet-parity.md`, pinned at 0.13.2 and checked by `CatalogueConsistencyTests` against `ParityCases`, `KnownGaps` and `Divergences`. Deferred rows link #94, #95 or #96. |
| 4 | Clean and broken variants for every in-scope entry, both tools agree except for catalogued `Divergences`, `KnownGaps` holds no type-level or slice entry, every `Divergences` entry is in the catalogue | PASS | `KnownGaps.Entries` is empty (`tests/Ariadne.NET.Parity.Tests/KnownGaps.cs:10`). `p-have-any-attributes` reads `covered`, with `:not` expressed as a rule exception with `attributes`, and the parity case `PARITY-PHAVEANYATTRIBUTES-NOT` runs both tools on its variants. #97 is closed. The 22 `Divergences` entries are each in the catalogue, which `CatalogueConsistencyTests` enforces. |
| 5 | Packs express naming pattern, base type, attribute, sealed, abstract, static, nested and slice rules; the pack JSON Schema and its conformance tests cover them | PASS | Selector traits and `is`/`isNot` from 4.2, `slices` from 4.3, `attributes` in exceptions from 4.7, all in `docs-site/static/schema/pack.schema.json` with `PackSchema` cases. |
| 6 | `modular-monolith` forbids one module from depending on another's internals, a fixture proves it, #32 closed | PASS | `MM-MODULES-INDEPENDENT` in `src/Ariadne.NET/BuiltinPacks/modular-monolith.yaml`, fixture `tests/fixtures/modular`; #32 is closed. |
| 7 | A migration guide maps each ArchUnitNET construct to pack YAML with Daedalus as the worked example | PASS | `docs-site/docs/migrating-from-archunitnet.md`, checked by `MigrationGuideSnippetTests` and `MigrationGuideTests`. Since 4.7 it also states that `assignableTo` used for `DoNotImplementInterface` or `DoNotInherit` exempts the named type itself. |
| 8 | Daedalus `main` references no ArchUnitNET package; its CI checks an Ariadne pack through `Ariadne.NET.Testing`; the only C# architecture tests left are `NuGetPackageRootPathTests`, the DI integration test and `RetainedArchitectureTests` | PASS | Daedalus.NET 423bba73: no `.csproj` or `Directory.Packages.props` names ArchUnitNET; `Directory.Packages.props` pins `Ariadne.NET.Testing` 0.9.1 and `Daedalus.Tests.Unit` references it; `AriadneArchitectureTests` checks `.architecture/rules.yaml`. The architecture test files left are `AriadneArchitectureTests`, `NuGetPackageRootPathTests`, `RetainedArchitectureTests` and, in the integration project, `CleanArchitectureTests`, which is the DI integration test: it boots the API host and checks that every `[Validate]` DTO has a registered validation adapter. CI on 423bba73 is green. |
| 9 | Member-level rule kinds are filed as one issue | PASS | #94 "ArchUnitNET member-level rule kinds", open. |

## Pre-push reviews

No `docs/pre-push-review-*.md` report exists. Warning, not a failure, as in the previous audit: each phase had per-task reviews and a final branch review, and CodeRabbit reviewed the phase PRs. For 4.7, CodeRabbit requested changes on #136, they were fixed, and it approved.

## Release check

`docs/planning/CONVENTIONS.md` says `Milestone completion tags a release: no`; release-please owns tagging, so the criterion is skipped. The milestone's last feature is already released in 0.9.2.

## Gaps

None.

## Open follow-ups, not blocking

- #132: 13 build warnings since `Microsoft.Build.Framework` and `Microsoft.NET.StringTools` moved to 18.12.2.
- #94 member-level kinds, #95 attribute-argument and attribute allow-list kinds, #96 dependency predicates and `DependOnAny`: deferred ArchUnitNET kinds.
- Renovate PR #20, YamlDotNet 18, is red.
