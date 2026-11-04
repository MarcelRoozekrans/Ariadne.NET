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
