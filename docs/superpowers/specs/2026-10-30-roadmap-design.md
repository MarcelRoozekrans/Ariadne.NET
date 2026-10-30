# Roadmap Design — Ariadne.NET (formerly Architecture Governance Engine)

> Filled in from `templates/roadmap-design.template.md` by `plan-roadmap`. Inputs: the conversation of 2026-10-30, `docs/superpowers/specs/2026-10-30-codebase-map.md`, and the READMEs of Rag.NET, Minos.NET, Mnemosyne.NET, Daedalus.NET and roslyn-codelens-mcp.

**Date:** 2026-10-30
**Author:** Claude, with Marcel Roozekrans
**Stage:** roadmap (whole project)

## Goal

Turn the architecture governance engine into **Ariadne.NET**, the member of the Cretan family of .NET AI libraries that answers *"where does this belong, and is it allowed here?"*, beside Rag.NET (what do we know?), Minos.NET (what should be decided?) and Mnemosyne.NET (how did we solve this before?). Ariadne becomes **the single place architecture rules are defined** for the family and its consumers. Rules are declarative YAML packs that any consumer can read and evaluate: Thalos agents and Daedalus manufacture runs in-process, coding agents over MCP, roslyn-codelens, and test runs that fail the build. Success at project level: Daedalus has replaced its ArchUnitNET architecture tests with an Ariadne pack, and agents use Ariadne to place and check code.

## Target Users / Stakeholders

- **Primary: agents in the family's runtime.** Thalos agents and Daedalus manufacture runs ask Ariadne where a component goes, and check the result before a run is accepted.
- **Primary: maintainers of .NET solutions**, starting with the user's own (Daedalus first). They want their architecture rules in one readable file, enforced in CI and visible to their agents.
- **Secondary: coding agents in an editor** (Claude Code, Copilot), reaching Ariadne over MCP and through a superpowers-extensions plugin.
- **Secondary: sibling tools.** roslyn-codelens's `check_architecture` reads Ariadne packs instead of its own rule format.
- **Out of scope:** non-.NET codebases, runtime enforcement of architecture, and generating method bodies or behaviour. Ariadne governs structure, never behaviour.

## Decisions taken in the brainstorm

- **Rename to Ariadne.NET** and adopt the family shape. Ariadne's thread leads through the Labyrinth that Daedalus built. Nothing is published yet, so the rename is a find-and-replace now and a breaking package-ID change later.
- **Library first.** The product is a set of NuGet packages; the MCP server, CLI and dotnet tool are surfaces of it, as in Rag.NET.
- **Ariadne is the central rule source.** The rules in Daedalus's ArchUnitNET tests and roslyn-codelens's `check_architecture` move to Ariadne packs over time. The switch-over itself is work in those repos.
- **Rules stay YAML only.** Every rule must be readable and checkable everywhere: in-process, over MCP, in roslyn-codelens and in tests. Ariadne therefore grows a deliberate vocabulary of general rule types instead of allowing C# rule classes. Checks that need a running DI container stay as ordinary tests in the consuming repo.
- **Order A: prove the format on Daedalus before anyone depends on it.** Daedalus's 34 ArchUnitNET tests are a real rule set with known answers. They were reviewed on 2026-10-30, and about 33 of them map to rule types Ariadne will need.

## Approaches considered for the order

- **A — Daedalus first (chosen).** Rename, then rule engine v2, then fail-the-build delivery, then Daedalus parity, then agent surfaces, then ecosystem. The YAML format is tested against 34 real rules before agents, Thalos or roslyn-codelens depend on it.
- **B — Agent first.** Rename, then generation and MCP for agents, then tightening the rules. Value arrives sooner, but agent use would lock the YAML format in before its vocabulary is complete.
- **C — Format first.** Design the whole schema up front, then build against it. Clean on paper, but every rule type would be designed without a real codebase pushing back.

## Calls made while writing this up

These were settled by Claude, not discussed; correct any of them.

- **Engine input contract (M2.1):** the core engine analyses a Roslyn `Compilation`, so a Thalos agent can call it in-process without a project on disk. Loading a solution or project path (MSBuildWorkspace or built assemblies) is a convenience in the CLI and `Ariadne.NET.Testing`. M3 and M5 may assume this split.
- **No v1 schema compatibility:** nothing is published, so schema v2 is a clean break. There is no dual-version loader; the 10 built-in packs and this repo's own `.architecture/rules.yaml` are migrated to v2 in M2.2.
- **Code generation stays where it is** through M1 to M4: in the `Ariadne.NET` engine package, converted to Results with the rest in M1.3. Its role is revisited in M5 (see Open Questions). There is no REST host any more; it was removed before this roadmap.
- **Reference repo for M1.4:** copy Rag.NET's setup (GitHub Actions CI, codecov, Docusaurus on GitHub Pages, release-please, Family section). This repo already has the Docusaurus site and release-please workflow.
- **External dependencies:** ZeroAlloc Results is already used by Rag.NET and Minos.NET; Minos.NET is at 1.0 with a stable API. M5.3 depends on Thalos's tool API, which is still moving (56 open issues), so M5.3 and M5.4 may slip without blocking the rest of M5.

## Top-Level Success Criteria

- `Ariadne.NET` packages are on NuGet under the family conventions: separate Abstractions and core packages, ZeroAlloc Results, a docs site, and a "Family" section in the README.
- A Daedalus pack reproduces the verdicts of Daedalus's ArchUnitNET architecture tests. The parity harness shows both flag the same deliberately broken variants, and no rule matches zero types without failing. Ariadne's part is done at parity plus the migration guide; Daedalus deleting its ArchUnitNET tests is that repo's own work *(Claude's call)*.
- A solution can fail its CI build on an Ariadne violation with one test call or one CLI command, and gets a JSON or SARIF report.
- A Thalos agent and Claude Code can each ask Ariadne where a component belongs and why a placement is rejected, and get the rule's stated reason back.
- roslyn-codelens can evaluate an Ariadne pack, so there is one rule format across the user's tools.

## Milestones

### Milestone 1: Ariadne.NET identity and family shape

**Goal:** The engine is renamed to Ariadne.NET, restructured into the family's package shape and published for the first time.

**Phases:**

1. Rename solution, namespaces, packages, CLI and docs from ArchGov to Ariadne.NET — `Surface: Refactor`
2. Split packages: `Ariadne.NET.Abstractions` (schema and loader), `Ariadne.NET` (engine), surfaces — `Surface: Refactor`
3. Adopt family conventions: ZeroAlloc Results instead of exceptions, ZeroAlloc analyzers clean, AOT where the dependencies allow it — `Surface: Refactor`
4. Publish: GitHub repo, CI with codecov, first release-please release, docs site, Family README section — `Surface: Infra`

### Milestone 2: Rule engine v2

**Goal:** Packs can express and enforce everything the Daedalus rules need, checked against the real type graph.

**Phases:**

1. Semantic dependency analysis over a Roslyn compilation, replacing the `using`-directive scan — `Surface: Backend`
2. Rule schema v2: versioned, with `forbid` and `allowOnly` at layer, namespace and type level, exception lists, and a `because` reason on every rule — `Surface: Backend`
3. Enforce placement and code-feature rules, so YAML-defined rules actually run — `Surface: Backend`
4. New rule types: banned packages and cross-layer name collisions — `Surface: Backend`
5. Vacuity detection: a rule that matches no types fails instead of passing silently — `Surface: Backend`
6. Pack reference docs and a JSON Schema for editor validation — `Surface: Docs`

### Milestone 3: Fail the build

**Goal:** Any .NET solution can enforce its Ariadne packs in tests and CI.

**Phases:**

1. `Ariadne.NET.Testing`: one call in a test that checks all packs against the solution — `Surface: Backend`
2. Machine-readable reports: JSON and SARIF from the CLI and the testing package — `Surface: Backend`
3. Ship the CLI as a dotnet tool (`ariadne`) — `Surface: Infra`
4. CI examples for GitHub Actions — `Surface: Docs`

### Milestone 4: Daedalus parity

**Goal:** Ariadne proves it can replace Daedalus's ArchUnitNET architecture tests.

**Phases:**

1. Write the Daedalus pack covering the structural and project-specific rules — `Surface: Mixed`
2. Parity harness: run Ariadne and ArchUnitNET on a Daedalus checkout, including deliberately broken variants, and compare verdicts — `Surface: Backend`
3. Close the vocabulary gaps the harness finds — `Surface: Backend`
4. Migration guide from ArchUnitNET to Ariadne — `Surface: Docs`

### Milestone 5: Agent surfaces

**Goal:** Agents in the family runtime and in editors use Ariadne while writing code.

**Phases:**

1. MCP server on rule engine v2, published as a dotnet tool and npx package like roslyn-codelens — `Surface: Infra`
2. Agent-facing tools: "where does this go", and violations explained with the rule's reason — `Surface: Backend`
3. Thalos tool package, so agents call Ariadne in-process — `Surface: Backend`
4. `Ariadne.NET.Minos`: choose a component role from a free-text request, with calibrated confidence — `Surface: Backend`
5. Claude Code plugin in superpowers-extensions that teaches agents when to call Ariadne — `Surface: Docs`

### Milestone 6: Ecosystem

**Goal:** Ariadne packs are the one architecture rule format across the user's tools.

**Phases:**

1. roslyn-codelens `check_architecture` evaluates Ariadne packs (work in that repo, with Ariadne supplying Abstractions) — `Surface: Backend`
2. NuGet as a pack-sharing source beside git sync — `Surface: Backend`
3. Docs site complete, with family cross-links and a pack catalogue — `Surface: Docs`

## Dependencies and Ordering Rationale

- **M1 → everything:** the rename and package split only get more expensive after the first release, and every later milestone ships under the new names.
- **M2 → M3:** fail-the-build delivery is only worth shipping once the rules are semantic and complete enough to trust in CI.
- **M3 → M4:** the parity harness runs Ariadne through its testing package and reports, so it tests the delivery path too.
- **M4 → M5 and M6:** agents, Thalos and roslyn-codelens should only start depending on the pack format after Daedalus parity has exercised it. This is the core of order A.
- **M5 and M6 can overlap** once M4 is done; neither depends on the other.
- **External:** M6.1 and the Daedalus switch-over are changes in other repos (roslyn-codelens, Daedalus) and are tracked there. M5.3 depends on Thalos's tool API; M5.4 on Minos.NET 1.x.

## Risk Register

| Risk | Impact | Mitigation |
|---|---|---|
| Semantic analysis needs a compilable solution (restore, MSBuildWorkspace) | Slow or brittle in agent loops and CI | Decide in M2.1 between MSBuildWorkspace and analysing built assemblies; keep the syntax-only path as a fast pre-check |
| Roslyn Workspaces and YamlDotNet are not Native AOT friendly | The "AOT" family convention cannot hold for the engine | Aim for AOT in `Ariadne.NET.Abstractions` only, and state the engine's status plainly |
| YAML-only rules hit a check no general rule type expresses | A consumer cannot fully move off its own tests | Accepted: such checks stay ordinary tests in the consumer; add a rule type only when it is general |
| Schema churn after other tools depend on it | Breaking changes for Daedalus, Thalos and roslyn-codelens | Versioned schema from M2.2; consumers start depending only after M4 |
| System.CommandLine is still a preview package | CLI API breaks on update | Pin the version; revisit at M3.3 |

## Open Questions

- **Build-time enforcement:** should M3 also ship a Roslyn analyzer, so violations show as build diagnostics in the IDE, or are the testing package and CLI enough? To be settled in M3's milestone brainstorm.
- **Code generation:** does the existing generator and workspace scaffolding stay a core feature of Ariadne, or does it move behind the agent surfaces in M5?
- **Mnemosyne integration:** would recalling earlier placements through Mnemosyne.NET add value beyond the pack itself? Not on the roadmap until a concrete use appears.

## Appendix: Daedalus architecture tests mapped to rule types

From `tests/Daedalus.Tests.Unit/Architecture/CleanArchitectureTests.cs` (34 tests) and `tests/Daedalus.Tests.Integration/Architecture/CleanArchitectureTests.cs` (1 test), reviewed 2026-10-30. This is the input for M2.2's vocabulary and M4.1's pack.

| Rule type needed | Daedalus tests | Count |
|---|---|---|
| Layer dependencies | `DomainLayer_ShouldNotDependOn_{Application,Infrastructure,Api}Layer`, `ApplicationLayer_ShouldNotDependOn_{Infrastructure,Api}Layer`, `InfrastructureLayer_ShouldNotDependOn_ApiLayer` | 6 |
| `forbid` external namespace per layer or project | `DomainLayer_ShouldNotDependOn_{Thalos,EfCore,ZeroAllocOutbox,Cronos,AgentsProject}`, `ApplicationLayer_ShouldNotDependOn_{Thalos,AgentsProject}`, `AgentsProject_ShouldNotDependOn_ApiLayer`, `Infrastructure_ShouldNotDependOn_Thalos`, `WebLayer_ShouldNotDependOn_Thalos` | 10 |
| `allowOnly` for an external namespace | `OnlyAgentsProject_DependsOn_RagNet` | 1 |
| `allowOnly` for a type, with exceptions | `OnlySubagentRunExecutor_DependsOn_ISubagentRunner` (two named exceptions) | 1 |
| Placement by role | `Controllers_ShouldResideIn_ApiLayer`, `RepositoryInterfaces_ShouldResideIn_ApplicationLayer`, `RepositoryImplementations_ShouldResideIn_InfrastructureLayer` | 3 |
| Cross-layer name collision | `WebLayer_KeepsNoCopyOf_AnApplicationDto` | 1 |
| Banned packages | `No_project_references_ZeroAlloc_Scheduling`, `FluentValidation_is_absent_…`, `CSharpFunctionalExtensions_is_absent_…` | 3 |
| Non-vacuity checks, made unnecessary by vacuity detection | `SubagentRunnerType_IsLoaded_…`, `ApplicationDtoNames_CoverEveryDtoNamespace_…`, `AgentsProject_DoesDependOn_Thalos_…`, `MemoryRagNetAdapter_DoesDependOn_RagNet_…`, `SkillsAssembly_IsLoaded_…`, `ChannelsAssemblies_AreLoaded_…`, `EfCoreAssembly_IsLoaded_…`, `ZeroAllocOutboxAssembly_IsLoaded_…`, `CronosAssembly_IsLoaded_…` | 9 |
| **Stays a Daedalus test** (needs the running DI container) | Integration: `Every_validated_dto_has_a_registered_validation_adapter` | 1 |

All 34 unit tests map to a planned rule type or to vacuity detection; the one integration test stays in Daedalus.
