# Milestone 3 Design — Fail the build

> Filled in from `templates/milestone-design.template.md` by `new-milestone`. Inputs: `docs/superpowers/specs/2026-10-30-roadmap-design.md` (Milestone 3), the Milestone 3 block in `docs/planning/ROADMAP.md`, a read of the CLI, the report types and the Workspaces package on `main` at `c574876`, and two answers from the owner on 2026-11-10.

**Date:** 2026-11-10
**Milestone:** 3
**Stage:** milestone (one milestone, multiple phases)
**Status:** approved 2026-11-10

## Goal

Any .NET solution can enforce its Ariadne packs in a test and in CI. One call in a test project checks every pack under `.architecture/` against the solution and fails the test with each broken rule's id, pack and `because`. The CLI and the testing package emit the same machine-readable reports in JSON and SARIF. The CLI ships as the `ariadne` dotnet tool, which also gives the agent hooks of phase 5.6 the command they assume is on `PATH`. GitHub Actions examples show a build failing on a violation and the violations appearing as code-scanning alerts. At the end of the milestone Milestone 4 can run Ariadne on a Daedalus checkout the same way a user's CI would.

## Where things stand today

Read from `main` on 2026-11-10:

- `ISolutionValidator.ValidateAsync(pack, path)` checks one resolved `RulePack`. The CLI `validate` reads only `.architecture/rules.yaml` from the current directory (`RulesCommand.ResolveRulesFromFile`). Nothing finds several packs.
- `ArchitectureReport` and `ArchitectureViolation` already carry rule id, kind, message, `because`, source location and target frameworks. A violation does not say which pack it came from, and there is no severity.
- `ariadne validate` prints text only. Exit codes are 0 clean, 1 violations, 2 could not run.
- `Ariadne.NET.Workspaces` needs an installed .NET SDK (MSBuildLocator) and makes every referencing project add `Microsoft.Build.Framework` with `ExcludeAssets="runtime"`, or the build fails with `MSBL001`.
- `Ariadne.NET.Cli` is `IsPackable=false`. CI has a `pack-validate` job that packs the three published packages and restores `tools/release/consumer` from a local feed, and a `publish-nuget` job on a tag dispatch.
- The hooks installed by `init --agent claude` call `ariadne hook ...`, so the command must be on `PATH`. Until the tool ships, the hooks item of Milestone 5 stays unticked.

## Decisions

Taken with the owner on 2026-11-10:

- **"All packs" means every pack file under `.architecture/`.** Each `*.yaml` directly in that folder is loaded and resolved on its own, checked against the solution, and the results are merged into one report. `rules.yaml` alone keeps working. Each violation names the pack it came from. The CLI and the testing package use the same discovery.
- **Roadmap order, with the shared work in phase 3.1.** 3.1 adds discovery and the pack-named violation and builds `Ariadne.NET.Testing` on them. 3.2 adds the writers. 3.3 packs the tool. 3.4 adds the CI examples.

## Approaches considered for the order

- **Roadmap order, shared work in 3.1 (chosen).** The CLI and the testing package share one discovery path from the first phase, and no phase waits on a later one.
- **Reports first.** The report model settles before anything formats failures, but a build cannot fail from a test until 3.2, and the roadmap's order changes.
- **Tool first.** Hooks and CI examples get a real command sooner, but the tool ships with text output and one pack and is repacked twice.

## Calls made while writing this up

These were settled by Claude, not discussed; correct any of them.

- **Pack discovery is non-recursive and sorted by file name.** Only `*.yaml` and `*.yml` directly in `.architecture/`. A pack that fails to load or resolve fails the whole check with `Invalid` and names the file; a partial check would pass a build that should fail. Two packs may define the same rule id, because they are resolved separately; the pack name tells them apart.
- **A pack that a sibling imports is not checked on its own.** `extends` accepts `builtin://` names and local `import:` files (`PackOperations.LoadReference`). When a pack in `.architecture/` is imported by another pack in the same folder, it is a base: it is checked through the importer and listed in the text report as "covered by <importer>", so its rules never run twice. A base meant to be checked alone is not importable by a sibling; that is a limit, stated in the docs.
- **No packs is an error, never a pass.** Missing `.architecture/` or no `*.yaml` in it returns `NotFound` (CLI exit 2, test failure). `.architecture/` is found by starting in the solution's folder and walking up to the first ancestor that has one; the current directory no longer matters, which is what a test run needs, since its working directory is under `bin/`. For the CLI this replaces "the current directory" with "the solution's folder and above".
- **Hooks and MCP switch to the same check in 3.1.** `StopHook` and MCP `ValidationTools` call the multi-pack check, so they cannot disagree with the CLI about "all packs". `IProjectRules` stays for the hooks' other uses.
- **`ArchitectureViolation.Pack` is optional** (`string Pack { get; init; } = ""`), so adding it is not a breaking change and the release is a plain `feat`. The single-pack API sets it from the pack's name too.
- **Pack identity is the pack's `name`.** Two files in `.architecture/` with the same `name` fail the check with `Invalid`, naming both files, because a report that cannot tell packs apart is useless. Output shows the name; the load error shows the file.
- **SARIF paths are relative to the repository root**: the nearest ancestor of the solution folder that has a `.git` entry, else the solution folder. The run carries `originalUriBaseIds` with `%SRCROOT%`, so code scanning can attach alerts when the solution is not at the root.
- **SARIF schema check.** The SARIF 2.1.0 schema is vendored under `tests/` and a test-only JSON Schema package validates against it; no network. ZeroAlloc.Serialisation is not referenced anywhere yet, so the first task of 3.2 is a short spike on whether it can write the SARIF shape; if not, `Utf8JsonWriter` is used and the call is recorded in the design.
- **`Microsoft.Build.Framework`.** The testing package targets net10 only and is test-framework agnostic; its own tests use xUnit. If `buildTransitive` props cannot supply the reference, the package fails fast with an error naming the exact line to add, and the docs say the same; the Definition of Done then reads as "one call plus that documented line".
- **Fixtures.** A new `tests/fixtures/twopacks` holds `.architecture/` with one clean and one broken pack for the multi-pack tests. The CI proof job packs the tool and runs it on the existing `tests/fixtures/broken` fixture, restoring normally on the runner, and asserts exit code 1.
- **The tool bundles its project references** in its `tools/` folder, as `PackAsTool` does by default, rather than depending on the three published packages. Its version stands alone; `publish-nuget` pushes it as the fourth package, and `pack-validate` installs it from the local feed.
- **Where the code lives.** Discovery, the multi-pack validator and the report writers go in the existing packages (`Ariadne.NET` for the report model and writers, `Ariadne.NET.Workspaces` for the solution-level check), so the CLI and the testing package stay thin. `Ariadne.NET.Testing` is a new, packable project that references `Ariadne.NET.Workspaces`.
- **`ArchitectureViolation` gets a `Pack` string** (the pack's `name`). The report gets no other new field. No severity: every violation is an error, and SARIF `level` is `error`.
- **The testing API is framework-agnostic.** It returns the merged `ArchitectureReport` and has an assert form that throws one exception whose message is the text report. xUnit, NUnit and MSTest all show that as a plain failure; no dependency on any of them. The solution is found by walking up from the test assembly's folder to the nearest single `.sln` or `.slnx`, or taken as an explicit path.
- **JSON shape is Ariadne's own and versioned** (`"schema": 1` at the top), with the report fields as they are plus `pack`. SARIF is version 2.1.0, one run, one `reportingDescriptor` per rule id seen with the `because` as its help text, locations as repository-relative URIs. A test validates the output against the SARIF 2.1.0 JSON schema.
- **CLI flags:** `validate [path] --format text|json|sarif` (default `text`) and `--output <file>`. With `--output` unset, JSON and SARIF go to stdout and the summary lines go to stderr, so stdout stays parseable. Exit codes do not change.
- **Writers use ZeroAlloc.Serialisation**, as the roadmap note says. If it cannot express the SARIF shape without reflection, the phase says so and falls back to `Utf8JsonWriter`, and that is recorded as a call.
- **The tool.** Package id `Ariadne.NET.Cli`, tool command `ariadne`, framework-dependent on .NET 10, no AOT or trimming because MSBuild loads at run time. It bundles `Ariadne.NET.Hooks` and the other projects it references as it does today. `pack-validate` installs it from the local feed and runs `ariadne --version` and a `validate` on a fixture; `publish-nuget` pushes it as the fourth package.
- **CI examples.** Two: a workflow using the tool (`dotnet tool install`, `ariadne validate --format sarif --output`, `github/codeql-action/upload-sarif`) and a plain `dotnet test` workflow using the testing package. This repo's own CI gains a job that runs the packed tool on a deliberately broken fixture and asserts exit code 1, so the "fails the build" claim is tested and not only documented.

## Definition of Done

- [ ] All planned phases complete
- [ ] All tests passing
- [ ] One call in a test (plus the package's documented MSBuild reference line, if it cannot ship it) checks every pack under `.architecture/` against a solution and fails with the rule id, pack name and `because` of each violation; a fixture with two packs, one clean and one broken, proves it
- [ ] `ariadne validate`, the stop hook and MCP validation check the same packs and name the pack per violation
- [ ] CLI and testing package emit JSON and SARIF 2.1.0; a test validates the SARIF against the published schema
- [ ] `ariadne` installs with `dotnet tool install` from the packed local feed in CI, and `publish-nuget` includes the package
- [ ] A GitHub Actions example fails a build on a violation, and a job in this repo's CI proves the failing exit code on a broken fixture
- [ ] The docs site documents the testing package, the report formats, the tool and the CI examples

## Phases

1. **Phase 3.1: Ariadne.NET.Testing** — `Surface: Backend`
   - **Goal:** One call in a test checks every pack under `.architecture/` against the solution, and the CLI `validate` does the same, with the pack named on each violation.
2. **Phase 3.2: JSON and SARIF reports** — `Surface: Backend`
   - **Goal:** Machine-readable reports from the CLI and the testing package.
3. **Phase 3.3: dotnet tool** — `Surface: Infra`
   - **Goal:** Ship the CLI as the `ariadne` dotnet tool.
4. **Phase 3.4: CI examples** — `Surface: Docs`
   - **Goal:** GitHub Actions examples that fail a build on a violation, tested in this repo's CI.

## Dependencies on Prior Milestones

- Depends on: Milestone 2, complete and released as 0.7.0 (the v2 engine, `Ariadne.NET.Workspaces`, the pack JSON Schema).
- Phase 3.2 builds on 3.1's pack-named violation; 3.3 builds on the CLI flags of 3.2 for its install check; 3.4 builds on 3.2 and 3.3.
- External: none. ZeroAlloc.Serialisation is already in the local NuGet cache.

## External Constraints

- The testing package and the tool need an installed .NET SDK at run time. Hosted GitHub runners have one.
- System.CommandLine is on a release candidate (`3.0.0-rc.3`); the tool inherits that until it is stable.
- Do not merge release-please or Renovate PRs; the owner does.

## Risk Areas

| Risk | Impact | Mitigation |
|---|---|---|
| The `Microsoft.Build.Framework` reference every consumer must add makes the testing package awkward | A test project fails with `MSBL001` on first use | 3.1 tries to ship the reference through the package's `buildTransitive` props. If MSBuild rules forbid it, the docs and the error message name the line to add, and the limit is recorded |
| ZeroAlloc.Serialisation cannot write the SARIF shape | The writer needs a different library | The phase falls back to `Utf8JsonWriter` and records it |
| Tool packaging bundles the wrong MSBuild assemblies | `ariadne validate` fails after `dotnet tool install` | The `pack-validate` job installs the tool and runs `validate` on a fixture, not only `--version` |
| Absolute paths leak into SARIF | Code scanning shows no locations | Paths are made relative to the solution folder; a test covers a nested project |
| A pack that cannot load hides the other packs' results | A broken build passes | Any load failure fails the whole check, as decided above |

## Open Questions

None blocking. The builder check below may add some.
