# Milestone 3 audit — Fail the build

**Date:** 2026-11-11
**Verdict:** PASS
**Audited at:** `main` 43d3917, release 0.7.1 on nuget.org

## Criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | All planned phases complete | PASS | Phases 3.1 to 3.4 have every plan box ticked and are `[complete]` in ROADMAP.md and MILESTONE.md. They were built as stacked PRs #68, #72, #73 and #76 and reached `main` together as #77, followed by #81 for two review findings in the example. |
| 2 | All tests passing | PASS | `dotnet test src/Ariadne.NET.slnx`: 1198 + 214 + 56 + 125 + 24 + 128 passed, 0 warnings. CI on `main` 43d3917 is green on ubuntu and windows. Local runs need SDK 10 pinned, for the temporary projects too: with the side-by-side .NET 11 SDK on the VM, two `SolutionLoaderTests` fail otherwise. Tracked in #75; CI installs only 10.x. |
| 3 | One call in a test checks every pack and fails with rule id, pack and `because`; a two-pack fixture proves it | PASS, with no extra line | `ArchitectureCheck.AssertValid` in `Ariadne.NET.Testing`; `tests/fixtures/twopacks` with `a-clean.yaml` and `b-broken.yaml`; `ArchitectureCheckTests.AssertValid_TwoPacks_ThrowsWithRuleIdPackAndBecause`. The package ships its MSBuild handling through `buildTransitive/Ariadne.NET.Testing.targets`, so consumers add no reference line: see the spike outcome in the 3.1 design. |
| 4 | `ariadne validate`, the stop hook and MCP validation check the same packs and name the pack per violation | PASS | All three use `IPackSetValidator`; `ArchitectureViolation.Pack`. Each host has a `twopacks` test: `ExitCodeTests`, `StopHookTests`, `ValidationToolsTests` and `ValidateSourceResultTests`. |
| 5 | CLI and testing package emit JSON and SARIF 2.1.0; a test validates the SARIF against the published schema | PASS | `JsonReportWriter`, `SarifReportWriter`; `validate --format json\|sarif --output`; `ArchitectureCheck.WriteReport` and the `AssertValid` report overloads. `tests/Ariadne.NET.Tests/Reports/SarifSchema.cs` validates against the vendored `sarif-schema-2.1.0.json`. |
| 6 | `ariadne` installs with `dotnet tool install` from the packed local feed in CI, and `publish-nuget` includes the package | PASS | `PackAsTool` with command `ariadne`; `pack-validate` installs the tool from the local feed and runs it; the 0.7.1 publish run pushed `Ariadne.NET.Cli` and `Ariadne.NET.Testing`, and both are on nuget.org. |
| 7 | A GitHub Actions example fails a build on a violation, and a job in this repo's CI proves the failing exit code on a broken fixture | PASS | `examples/github-actions/ariadne-tool.yml` and `ariadne-test.yml`; the `fail-the-build` job expects exit 1 and a SARIF result on `tests/fixtures/broken`, exit 0 on `tests/fixtures/clean`, and `publish-nuget` needs it. |
| 8 | The docs site documents the testing package, the report formats, the tool and the CI examples | PASS | `how-it-works/13-testing.md`, `how-it-works/14-reports.md`, "Install the tool" in `getting-started.md`, `ci-github-actions.md`. |

## Pre-push reviews

No `docs/pre-push-review-*.md` report exists. Warning, not a failure: each phase had per-task reviews and a whole-branch final review in its subagent-driven run, and CodeRabbit reviewed #77 and #81. Code quality was not independently reviewed through `pre-push-review`.

## Release check

Conventions say `Milestone completion tags a release: no`; release-please owns tagging, so the criterion is skipped. Release 0.7.1 lists the milestone's eleven features through the commit override in #77.

## Open follow-ups

Filed during the milestone, none blocking: #66 hooks find the project root only by `rules.yaml`, #67 SolutionLoader throws when the build host folder is missing, #70 usage errors exit 1, #71 SARIF results without a location are invisible in code scanning, #75 the .NET 11 SDK and a repo `global.json`.
