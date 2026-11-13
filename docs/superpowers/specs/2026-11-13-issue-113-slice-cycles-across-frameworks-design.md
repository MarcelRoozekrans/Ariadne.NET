# Issue #113: slice cycles across compatible target frameworks

Approved by the owner on 2026-11-13. Small change, short design.

## Intent

`freeOfCycles` misses a slice cycle when the edges of the cycle sit in different but compatible target frameworks. `SliceRules.Scope.Build` puts a graph only in the scope whose framework string matches its own. In the issue's example, Infra (`net10.0;net11.0`) compiles against Billing's `net9.0` variant, so no scope holds both the Infra → Billing edge and the Billing → Orders edge. The fix: a scope for consumer framework F holds, for each producer project, the variant that F's build compiles against.

## Goals and non-goals

- Goal: in the issue's example, `freeOfCycles` reports the cycle.
- Goal: each scope holds one variant per project, so `Scope.Edges` never counts a reference twice.
- Non-goal: NuGet's nearest-framework rule. The engine stays free of NuGet and MSBuild.
- Anti-goal: losing the cycles today's scopes find, such as two sibling projects with no reference between them that close a cycle through a shared library.

## What we found

A probe loaded a three-project solution through `SolutionLoader`: Core on `netstandard2.0`, Billing on `netstandard2.0;net9.0` referencing Core, and Infra on `net10.0;net11.0` referencing Billing. MSBuildWorkspace gives each C# project reference to the consumer's `Compilation` as a `CompilationReference`. Its `Compilation` is the same object (`ReferenceEquals`) as the `AnalyzedProject.Compilation` of the producer variant the build uses: both Infra variants point at Billing `net9.0`. This still holds when the producer runs a source generator.

Two alternatives fail:
- Symbol equality, because across frameworks the target type's assembly is a retargeting symbol.
- Assembly identity, because it can't tell the variants apart.

## Approaches

1. **Match `CompilationReference` objects to the producer's `AnalyzedProject` in the engine (chosen).** Exact, it needs no loader wiring, and the engine already holds the compilations.
2. Have the loader pass an explicit mapping on `AnalyzedProject`. This adds public API that approach 1 makes unnecessary.
3. Apply NuGet's nearest-framework rule. This adds a NuGet dependency, or a copy of its rule, to the engine.

For the scope's contents, we considered one scope per root build (a variant nobody references, plus everything it references). We rejected it because it loses cycles between sibling projects that today's framework scopes find.

## Design

- `ArchitectureValidator` gives `SliceRules` the project name of each graph and the indices of the graphs it references. The references are found by matching each `CompilationReference` in `project.Compilation.References` to another entry's `Compilation` by reference. A reference that matches nothing is ignored: a metadata reference, a project outside the loaded set, or a compilation built in code. With no matches, the scopes are what they are today.
- `Scope.Build` starts the scope for framework F as today: every graph with framework F, then every graph without a framework. It then walks references breadth-first from those graphs, in graph order. A referenced graph joins the scope when its project has no variant in the scope yet. A project's own F variant always wins, so it keeps one variant per scope. When no project names a framework, the single scope is unchanged.
- Both `freeOfCycles` and `independent` use the new scopes.
- `TargetFrameworks` keeps its meaning: the scopes, which are consumer builds, that found the violation, and empty when every scope did. Owner decision 2026-11-13. In the example, the Billing `net9.0` → Orders cycle edge reports `net10.0, net11.0`. An `independent` edge in Billing `net9.0` now also names `net10.0` and `net11.0`, because those builds contain it.

## Left to the builder

- The internal shape of the per-graph input to `SliceRules.Check`, which is internal.
- Test layout: engine tests in `SliceRulesCycleTests` and `SliceRulesTests` that link compilations with `ToMetadataReference()`, and a Workspaces test on a new fixture that holds the issue's three projects.
- The docs-site sentence on slice scopes, if there is one, and the `SliceRules.Check` doc comment.
