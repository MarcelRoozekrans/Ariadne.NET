# Milestone 3: Fail the build

**Status:** complete
**Started:** 2026-11-10
**Completed:** 2026-11-11

## Goal

Any .NET solution can enforce its Ariadne packs in a test and in CI. One call in a test project checks every pack under `.architecture/` against the solution and fails the test with each broken rule's id, pack and `because`. The CLI and the testing package emit the same machine-readable reports in JSON and SARIF. The CLI ships as the `ariadne` dotnet tool, which also gives the agent hooks the command they assume is on `PATH`. GitHub Actions examples show a build failing on a violation. See `docs/superpowers/specs/2026-11-10-milestone-3-design.md`.

## Definition of Done

- [x] All planned phases complete
- [x] All tests passing
- [x] One call in a test (plus the package's documented MSBuild reference line, if it cannot ship it) checks every pack under `.architecture/` against a solution and fails with the rule id, pack name and `because` of each violation; a fixture with two packs, one clean and one broken, proves it
- [x] `ariadne validate`, the stop hook and MCP validation check the same packs and name the pack per violation
- [x] CLI and testing package emit JSON and SARIF 2.1.0; a test validates the SARIF against the published schema
- [x] `ariadne` installs with `dotnet tool install` from the packed local feed in CI, and `publish-nuget` includes the package
- [x] A GitHub Actions example fails a build on a violation, and a job in this repo's CI proves the failing exit code on a broken fixture
- [x] The docs site documents the testing package, the report formats, the tool and the CI examples

## Phases

1. Phase 3.1 — Ariadne.NET.Testing [complete]
2. Phase 3.2 — JSON and SARIF reports [complete]
3. Phase 3.3 — dotnet tool [complete]
4. Phase 3.4 — CI examples [complete]
