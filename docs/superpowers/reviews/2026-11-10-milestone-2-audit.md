# Milestone 2 audit — Rule engine v2

**Date:** 2026-11-10
**Verdict:** PASS on the second run. The first run failed on one documentation criterion; PR #60 closed it.
**Audited at:** release 0.7.0 on nuget.org

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | All planned phases complete | PASS | Phases 2.1 to 2.6 are `[complete]` in ROADMAP.md and MILESTONE.md. |
| 2 | All tests passing | PASS | `dotnet test src/Ariadne.NET.slnx`: 1136 + 208 + 49 + 111 + 99 passed before PR #62; 1605 after it. CI green on #60 and #62. |
| 3 | Every Daedalus-appendix rule type expressible and enforced, clean and broken fixture | PASS | `tests/fixtures/daedalus/{clean,broken}`, `daedalus.rules.yaml`, `DaedalusFixtureTests`. |
| 4 | Semantic type graph; `SourceValidator` deleted; CLI `validate` and MCP `validate_source` on the new engine; issue #1 closed | PASS | No `SourceValidator` in `src`; `ValidateCommand` and `ValidationTools` use `ISolutionValidator`; issue #1 is closed. |
| 5 | Workspaces loads sln, slnx and csproj; packed and published by CI | PASS | `pack-validate` job; all three packages on nuget.org. |
| 6 | A selector that matches no type fails unless `allowEmpty: true` | PASS | Phase 2.5, `VacuousRule` and `VacuousLayer`. |
| 7 | Pack can forbid namespaces, types and members per layer; reference shows SQL, crypto and HTTP confined to Infrastructure | PASS after PR #60 | First run: SQL only, FAIL. `rule-types.md` now has the section "Keeping sensitive APIs in Infrastructure" with all three rules; the pack was loaded through the CLI `rules` command. |
| 8 | Loader rejects unknown keys and rules without `because` | PASS | Strict loader from 2.2; `PackYamlReaderTests` and the invalid corpus of the 2.6 conformance tests. |
| 9 | All 10 built-in packs and `.architecture/rules.yaml` on schema v2; repo passes its own pack in CI | PASS | Every `BuiltinPacks/*.yaml` starts with `schema: 2`; `ci.yml` validates the solution against the repo pack. |
| 10 | JSON Schema in repo and on docs site; reference documents every rule type | PASS | `docs-site/static/schema/pack.schema.json`, `rule-types` and `editor-support` pages. |
| 11 | Ships to nuget.org in a release-please release | PASS | 0.7.0 released and on nuget.org; the notes list the one breaking change. |

## Pre-push reviews

No `docs/pre-push-review-*.md` report exists. Warning, not a failure: the phases used the review waves recorded in STATE.md. Code quality was not independently reviewed through `pre-push-review`.

## Release check

Conventions say `Milestone completion tags a release: no`; release-please owns tagging, so the criterion is skipped.
