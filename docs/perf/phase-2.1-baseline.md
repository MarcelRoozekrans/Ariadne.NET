# Phase 2.1 performance baseline

This file records how long it takes to load a solution and check it against the repo's rules with the phase 2.1 type graph, and how much the check allocates. Later phases compare against these numbers and explain any regression.

## What was measured

One warm-up run is done first and discarded. Each measured run then does two things in order:

1. Loads the solution with `ISolutionLoader` (MSBuild workspace, one compilation per project and target framework).
2. Checks the loaded projects with `IArchitectureValidator` against the repo's own `.architecture/rules.yaml`, resolved the way the CLI does.

The load time and the check time are timed separately with a `Stopwatch`. The check's allocations are the difference of `GC.GetTotalAllocatedBytes(true)` taken just before and just after `Validate`. Before each measured check the tool forces a full garbage collection, so the check starts from a clean heap. The allocation counter is process-wide, so background threads can add a little to the figure. Every loaded solution is disposed after its run. The tool is `tools/perf/Ariadne.NET.Perf`.

## Environment

| Item | Value |
| --- | --- |
| Date | 2026-11-04 |
| Commit | measured at the commit that adds this file; `git log -- docs/perf/phase-2.1-baseline.md` finds it |
| CPU | Intel Core Ultra 7 255H |
| Cores | 12 logical |
| OS | Debian GNU/Linux 13 (trixie) |
| `dotnet --version` | 10.0.403 |
| Configuration | Release |

The numbers come from a shared VM. Other work was running on it, so the timings are noisy and the allocation figures are the stable comparison. Compare medians, not single runs.

## Command

```
dotnet build src/Ariadne.NET.slnx -c Release
dotnet run --project tools/perf/Ariadne.NET.Perf -c Release --no-build -- src/Ariadne.NET.slnx --runs 5
```

Run it from the repository root, because the rules are read from `.architecture/rules.yaml` in the current directory.

## Results

Solution: `src/Ariadne.NET.slnx`, 12 projects, 199 types checked, 0 violations. The project and type counts include the perf tool itself.

The repo's own rule pack reports no violations on its own solution. Phase 2.2 migrates the pack, so this count may change.

| Metric | Median of 5 runs |
| --- | --- |
| Load time | 3008 ms |
| Check time | 837.0 ms |
| Check allocations | 313,218,272 bytes (about 299 MiB) |

Individual runs:

```
run 1: load 4494 ms, check 1192.0 ms, check allocated 313,294,608 bytes
run 2: load 3949 ms, check 885.1 ms, check allocated 313,218,272 bytes
run 3: load 3008 ms, check 837.0 ms, check allocated 313,243,744 bytes
run 4: load 2802 ms, check 819.6 ms, check allocated 313,175,936 bytes
run 5: load 2947 ms, check 764.5 ms, check allocated 313,178,376 bytes
```

The warm-up run is not listed. Run 1 above is the first measured run; the load time still drifts down over the first runs as the OS file cache warms.

## Phase 2.2

Phase 2.2 replaced the v1 layer map and rules with schema 2: selector-based layers, `LayerOverlap`, `forbid` and `allowOnly` checks. This section re-measures the same check the way the baseline above describes it.

### Environment

| Item | Value |
| --- | --- |
| Date | 2026-11-05 |
| Commit | `66565b8` plus the documentation changes of the commit that adds this section |
| CPU | Intel Core Ultra 7 255H |
| Cores | 12 logical |
| OS | Debian GNU/Linux 13 (trixie) |
| `dotnet --version` | 10.0.403 |
| Configuration | Release |

This is the same shared VM as the 2.1 run. The load average was about 1.5 before the run and 1.8 after it, so other work was running and the timings are noisy. The allocation figures are the stable comparison.

### Command

```
dotnet build src/Ariadne.NET.slnx -c Release
dotnet run --project tools/perf/Ariadne.NET.Perf -c Release --no-build -- src/Ariadne.NET.slnx --runs 5
```

### Results

Solution: `src/Ariadne.NET.slnx`, 12 projects, 224 types checked, 0 violations. The checked rules are the repo's own `.architecture/rules.yaml`: 4 layers selected by assembly and 2 `forbid` rules.

| Metric | Phase 2.1 | Phase 2.2, before caching | Change |
| --- | --- | --- | --- |
| Types checked | 199 | 224 | +25 (+12.6%) |
| Load time (median of 5) | 3008 ms | 2737 ms | -9% |
| Check time (median of 5) | 837.0 ms | 1012.7 ms | +21% |
| Check allocations (median of 5) | 313,218,272 bytes (about 299 MiB) | 412,696,312 bytes (about 394 MiB) | +32% |

Individual runs:

```
run 1: load 3073 ms, check 1099.7 ms, check allocated 412,800,192 bytes
run 2: load 2737 ms, check 998.3 ms, check allocated 412,699,024 bytes
run 3: load 2955 ms, check 1015.9 ms, check allocated 412,690,672 bytes
run 4: load 2460 ms, check 1012.7 ms, check allocated 412,696,312 bytes
run 5: load 2652 ms, check 985.5 ms, check allocated 412,691,104 bytes
```

The warm-up run is not listed.

### What changed

The load time does not depend on the rules, so its difference is the noise of a shared VM and a warmer file cache. Ignore it.

The check got slower and allocates more, for these reasons:

- The solution is larger. The solution includes its test projects, and phase 2.2 added the rule and selector types in Abstractions, the glob, selector, layer map and rule classes and the strict pack reader in the engine, and their tests, and removed the linter and the old code feature rule model. The net result is 224 types to check instead of 199. That is 12.6% more types, and the type graph is built per type.
- The engine does more per run. Besides the layer-dependency check it now looks for `LayerOverlap` on every source type and on every metadata type that is the target of an edge, and it evaluates every `forbid` and `allowOnly` rule against every edge. The repo pack now has 4 layers and 2 `forbid` rules. In 2.1 it declared 3 layers and extended clean-architecture, so it resolved to 7 layers under the v1 union merge, plus two code feature rules that nothing evaluated. The 2.1 layer lookup was cheaper per type (one namespace segment against 7 names), so the rule count alone does not say the 2.1 workload was smaller; the extra cost comes from the checks and the selector matching described here.
- Layers now select by namespace and assembly globs, applied to source and to metadata types, where v1 compared one namespace segment with the layer name. Each type's layer is cached per symbol, but a type that is only a target, such as a framework type, is now matched against every layer selector. The cache and the compiled globs are new allocations.

Per checked type, the allocations went from about 1.57 MB to about 1.84 MB, an increase of 17%. The check time per type went from about 4.2 ms to about 4.5 ms, an increase of 7%, which is within the noise of this VM. I did not profile the check to split the increase between the three causes above. The check is not a hot loop in normal use: it runs once per validation, next to a load that takes 2.5 to 3 seconds.

### After caching selector matches

The final review of phase 2.2 found that only the layer lookup was cached per symbol. A `forbid` or `allowOnly` target with namespace, assembly or type selectors matched again on every edge, and every match built the type's namespace string, and its full name for a type selector. An `allowOnly` rule could match three times per edge, exceptions matched again on every violating edge, and the validator built the same layer map twice per project. The fix caches each target's and each exception list's answer per symbol, builds a type's names once per run, and uses one layer map for the whole run. This section measures it.

Environment: the same VM, CPU, OS, SDK and configuration as above, on 2026-11-05, at commit `7a3cb4b` (after) and its parent `12beb80` (before). The load average was between 2 and 6 during these runs, higher than for the runs above, so the timings are noisier still. The command is the same. The "before" columns were measured in the same session, right before the "after" columns, so both sides saw the same machine.

Besides the repo's own pack, I ran a second pack written for this measurement, kept out of the repository: 4 layers selected by namespace, 2 `forbid` and 2 `allowOnly` rules with namespace and type selectors on both sides, and exceptions. It selects by name on every edge, which the repo pack, selecting by assembly, never does. The after commit adds two types to the solution, `TypeNames` and its tests, so it checks 226 types instead of 224.

| Pack | Metric (median of 5) | Before caching | After caching |
| --- | --- | --- | --- |
| Repo pack | Check time | 1028.9 ms | 1042.8 ms |
| Repo pack | Check allocations | 416,950,016 bytes | 419,041,608 bytes |
| Repo pack | Allocations per type | 1.861 MB | 1.854 MB |
| Name-selector pack | Check time | 1442.9 ms | 1052.6 ms |
| Name-selector pack | Check allocations | 424,411,656 bytes | 419,195,032 bytes |
| Name-selector pack | Allocations per type | 1.895 MB | 1.855 MB |

A third run with an empty pack, no layers and no rules, allocated 419,029,032 bytes for the check of the 226 types after caching: the same as with the repo pack. Almost all of the check's allocations come from building the type graph, which binds every syntax node through the semantic model, not from the rules.

What this shows:

- For the repo pack, caching changes nothing measurable. It selects by assembly name, which Roslyn already holds as a string, and its layer lookup was already cached. The differences above are two more types and noise.
- For a pack that selects by namespace and type name, the check allocates about 5 MB less, about 2% per type, because each type's names are built once instead of on every edge and rule. The check time median moved from 1443 ms to 1053 ms, but the before runs ranged from 1059 ms to 2042 ms on a loaded VM, so I do not claim a time gain from these numbers.
- The phase 2.2 increase over phase 2.1 described above is not explained by selector matching: an empty pack allocates as much as the repo pack. It sits in the type graph build over a larger solution, not in the rules. I have not profiled the graph build.
