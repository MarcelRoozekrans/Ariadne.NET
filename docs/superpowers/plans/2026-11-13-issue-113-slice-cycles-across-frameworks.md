# Slice cycles across compatible target frameworks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `freeOfCycles` finds a slice cycle whose edges sit in different but compatible target frameworks (issue #113).

**Architecture:** The validator matches each `CompilationReference` of a project's compilation to the `AnalyzedProject` whose `Compilation` it is (`ReferenceEquals`), and hands `SliceRules` each graph's project name and referenced graphs. `Scope.Build` seeds the scope for framework F as today, then adds, breadth-first, the referenced variant of every project that has no variant in the scope yet.

**Tech Stack:** C# / .NET 10, Roslyn, xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-11-13-issue-113-slice-cycles-across-frameworks-design.md`

## Global Constraints

- The engine (`src/Ariadne.NET`) gets no NuGet or MSBuild dependency; it uses only `Microsoft.CodeAnalysis` types it already references.
- One variant per project per scope; a project's own variant for the scope's framework always wins over a referenced one.
- A reference that matches no input graph is ignored; with no matches, scopes are exactly today's.
- `TargetFrameworks` keeps its meaning: the scopes that found the violation, empty when every scope did.
- Zero warnings (`TreatWarningsAsErrors`), one type per file (MA0048), `ConfigureAwait(false)` on awaits.
- No nested parentheses in commit message bodies.

## Review Focus

- A consumer whose framework variant of the producer exists but references another variant (`SetTargetFramework`): the scope keeps the own variant, counts once. Pinned in Task 1, `Referenced_variant_does_not_replace_the_projects_own_variant`.
- A `CompilationReference` to a compilation outside the run, or a plain metadata reference: ignored, no exception. Pinned in Task 1, `Reference_to_a_compilation_outside_the_run_is_ignored`.
- A variant reached only through another referenced variant: joins the scope. Pinned in Task 1, `Variant_referenced_through_a_referenced_variant_joins_the_scope`.
- `independent` counts an edge of a referenced variant in the consumer's scope. Pinned in Task 1, `Independent_edge_in_a_referenced_variant_counts_in_the_consumer_scope`.
- A real MSBuild load of the issue's layout. Pinned in Task 2.

---

### Task 1: Scopes follow compilation references

**Files:**
- Create: `src/Ariadne.NET/Analysis/ScopedGraph.cs`
- Modify: `src/Ariadne.NET/Analysis/SliceRules.cs` (`Check`, `SlicedTypes.Collect`, `Scope.Build`, doc comments)
- Modify: `src/Ariadne.NET/Analysis/ArchitectureValidator.cs` (`FrameworkGraphsOf` → builds `ScopedGraph`s)
- Modify: `docs-site/docs/how-it-works/rule-types.md:286`
- Test: `tests/Ariadne.NET.Tests/Analysis/SliceRulesCycleTests.cs`, `tests/Ariadne.NET.Tests/Analysis/SliceRulesTests.cs`, `tests/Ariadne.NET.Tests/Analysis/SlicesRuleToleranceTests.cs` (helpers only, if it calls `SliceRules.Check`)

**Interfaces:**
- Produces: `internal readonly record struct ScopedGraph(TypeGraph Graph, string? TargetFramework, string Project, IReadOnlyList<int> References)` and `public static List<ScopedGraph> ScopedGraph.Of(IReadOnlyList<AnalyzedProject> projects, IReadOnlyList<TypeGraph> graphs)`. `References` holds the indices, in `Compilation.References` order without duplicates, of the other inputs whose `Compilation` is the `CompilationReference.Compilation` of one of this project's references; self is never included.
- Produces: `SliceRules.Check(LayerMap layers, RulePack pack, IReadOnlyList<ScopedGraph> graphs)` replaces the tuple overload.

- [ ] **Step 1: Switch the test helpers to `ScopedGraph.Of`**

In each test file that calls `SliceRules.Check`, the helper builds `AnalyzedProject(compilation.AssemblyName ?? string.Empty, framework, compilation)` per input, a graph per project with `TypeGraphBuilder.Build`, then `SliceRules.Check(new LayerMap(pack), pack, ScopedGraph.Of(projects, graphs))`. It does not compile yet.

- [ ] **Step 2: Write the failing tests in `SliceRulesCycleTests`**

Pattern `Shop.(*).**`, rule `Acyclic()`. Compilations, all linked with `ToMetadataReference()`:
- `orders`: `namespace Shop.Orders { public class Order { } }`, assembly `Orders.Core`, framework null.
- `billingStd` and `billing8`: `namespace Shop.Billing { public class Invoice { public Shop.Orders.Order? O; } }` referencing `orders`, assembly `Billing`, frameworks `netstandard2.0` and `net8.0`.
- `infra9` and `infra10`: `namespace Shop.Orders.Infrastructure { public class Repo { public Shop.Billing.Invoice? I; } }` referencing `billing8` and `orders`, assembly `Orders.Infrastructure`, frameworks `net9.0` and `net10.0`.

```csharp
[Fact]
public void Cycle_through_a_compatible_framework_variant_is_found()
{
    var violations = Check(PackWith(Acyclic()), (orders, null), (billingStd, "netstandard2.0"), (billing8, "net8.0"), (infra9, "net9.0"), (infra10, "net10.0"));

    violations.Should().HaveCount(2).And.OnlyContain(v => v.Kind == ViolationKind.SliceCycle);
    violations.Select(v => (v.SourceType, v.Target)).Should().BeEquivalentTo(new[]
    {
        ("Shop.Billing.Invoice", "Shop.Orders.Order"),
        ("Shop.Orders.Infrastructure.Repo", "Shop.Billing.Invoice"),
    });
    violations.Should().OnlyContain(v => v.TargetFrameworks.SequenceEqual(new[] { "net10.0", "net9.0" }) && v.ReferenceCount == 1);
}

[Fact]
public void Referenced_variant_does_not_replace_the_projects_own_variant()
// infra10 references billing8 while billing10 (same source, net10.0) also exists: the net10.0 scope holds billing10 only,
// so the Billing → Orders edge has ReferenceCount 1 and both edges report TargetFrameworks empty
// (inputs: orders null, billing8 net8.0, billing10 net10.0, infra8 net8.0 → billing8, infra10 net10.0 → billing8).

[Fact]
public void Reference_to_a_compilation_outside_the_run_is_ignored()
// infra10 references a billing8 that is not an input; only orders (null) and infra10 (net10.0) are passed: no violation, no exception.

[Fact]
public void Variant_referenced_through_a_referenced_variant_joins_the_scope()
// ordersStd and ordersNet6: assembly Orders.Core, netstandard2.0 and net6.0, `namespace Shop.Orders { public class Order { } }`.
// shared8: assembly Shared, net8.0, `namespace Shop.Billing { public class Invoice { public Shop.Orders.Order? O; } }`, referencing ordersStd.
// glue10: assembly Glue, net10.0, `namespace Shop.Orders.Infrastructure { public class Repo { public Shop.Billing.Invoice? I; } }`,
// referencing shared8 only. The net10.0 scope gets shared8, then ordersStd through it: two SliceCycle violations, TargetFrameworks ["net10.0"].
```

Write the three commented tests with the inputs and assertions their comments give.

- [ ] **Step 3: Write the failing test in `SliceRulesTests`**

```csharp
[Fact]
public void Independent_edge_in_a_referenced_variant_counts_in_the_consumer_scope()
// Independent([ShopModules]); inputs: orders (null), billing8 (net8.0), infra10 (net10.0 → billing8), sources as in the cycle test.
// Billing.Invoice → Orders.Order is found in both scopes: TargetFrameworks empty (today it reports ["net8.0"]).
// Orders.Infrastructure.Repo → Billing.Invoice: TargetFrameworks ["net10.0"].
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test tests/Ariadne.NET.Tests --filter "FullyQualifiedName~SliceRules"`
Expected: build error on `ScopedGraph`, then, once Step 5's type exists, the new tests FAIL and the old ones pass.

- [ ] **Step 5: Implement `ScopedGraph` and the scope fill**

- `ScopedGraph.Of`: map each input's `Compilation` to its index with `ReferenceEqualityComparer.Instance`; for each project, walk `Compilation.References.OfType<CompilationReference>()` and add the matched index when it is not the project itself and not already listed.
- `SliceRules.Check` and `SlicedTypes.Collect` take `IReadOnlyList<ScopedGraph>`.
- `Scope.Build`: seed exactly as today (framework graphs, then unnamed graphs; one scope over all graphs when no framework is named, with no fill). Then, per named scope, a queue over its members in the order added: for each referenced index not in the scope whose `Project` has no member in the scope (`StringComparer.Ordinal`), add it and enqueue it. Track membership with a `HashSet<int>` for graphs and one for project names.
- `ArchitectureValidator.FrameworkGraphsOf` returns `ScopedGraph.Of(projects, graphs)` from the entries.
- Update the doc comments on `SliceRules.Check` and `Scope` to say a scope holds the variants its framework's build compiles against.
- `rule-types.md:286`: after "together with the projects that name none", add that it also holds, for a project without that framework, the variant the build compiles against, so a cycle is found when its edges sit in compatible frameworks such as `net9.0` and a `net8.0` dependency.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Ariadne.NET.Tests`
Expected: PASS, 0 warnings.

- [ ] **Step 7: Commit**

```bash
git add src/Ariadne.NET tests/Ariadne.NET.Tests docs-site/docs/how-it-works/rule-types.md
git commit -m "fix: find slice cycles across compatible target frameworks" -m "Refs #113"
```

### Task 2: Fixture solution with the issue's layout

**Files:**
- Create: `tests/fixtures/crossframework/CrossFramework.slnx`, `Core/Core.csproj` + `Core/Order.cs`, `Billing/Billing.csproj` + `Billing/Invoice.cs`, `Infra/Infra.csproj` + `Infra/Repo.cs`, `.architecture/rules.yaml`
- Modify: `tests/Shared/FixtureProjects.cs` (`RestoreTargets` gains `"crossframework/CrossFramework.slnx"`)
- Test: `tests/Ariadne.NET.Workspaces.Tests/CrossFrameworkSliceTests.cs`

**Interfaces:**
- Consumes: Task 1's behaviour through `SolutionValidator`.

- [ ] **Step 1: Create the fixture**

- `Core`: `netstandard2.0`, `namespace Cross.Orders { public class Order { } }`.
- `Billing`: `netstandard2.0;net8.0`, references Core, `namespace Cross.Billing { public class Invoice { public Cross.Orders.Order? O { get; set; } } }` with `<Nullable>enable</Nullable>` and `<LangVersion>latest</LangVersion>`.
- `Infra`: `net9.0;net10.0`, references Billing, `namespace Cross.Orders.Infrastructure { public class Repo { public Cross.Billing.Invoice? I { get; set; } } }`.
- `rules.yaml`: `schema: 2`, `name: crossframework`, one rule `CF-ACYCLIC` with `because: Slices form a hierarchy.` and `slices: { matching: ["Cross.(*).**"], freeOfCycles: true }`.

- [ ] **Step 2: Write the test, modelled on `ModularMonolithTests`**

```csharp
[Fact]
public async Task Cycle_across_compatible_frameworks_is_reported()
{
    var report = await ValidateAsync();

    report.SkippedProjects.Should().BeEmpty();
    report.Violations.Should().HaveCount(2).And.OnlyContain(v => v.Kind == ViolationKind.SliceCycle && v.RuleId == "CF-ACYCLIC");
    report.Violations.Select(v => (v.SourceType, v.Target)).Should().BeEquivalentTo(new[]
    {
        ("Cross.Billing.Invoice", "Cross.Orders.Order"),
        ("Cross.Orders.Infrastructure.Repo", "Cross.Billing.Invoice"),
    });
    report.Violations.Should().OnlyContain(v => v.TargetFrameworks.SequenceEqual(new[] { "net10.0", "net9.0" }));
}
```

- [ ] **Step 3: Run it**

Run: `dotnet test tests/Ariadne.NET.Workspaces.Tests --filter "FullyQualifiedName~CrossFrameworkSliceTests"`
Expected: PASS. To confirm it guards the fix, temporarily return `[]` from `ScopedGraph`'s reference matching and see it FAIL with no violations, then restore.

- [ ] **Step 4: Full run and commit**

Run: `dotnet build src/Ariadne.NET.slnx && dotnet test src/Ariadne.NET.slnx`
Expected: 0 warnings, all tests pass.

```bash
git add tests/fixtures/crossframework tests/Shared/FixtureProjects.cs tests/Ariadne.NET.Workspaces.Tests/CrossFrameworkSliceTests.cs
git commit -m "test: load a cross-framework slice cycle from a fixture solution" -m "Refs #113"
```
