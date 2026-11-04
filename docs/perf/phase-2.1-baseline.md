# Phase 2.1 performance baseline

This file records how long it takes to load a solution and check it against the repo's rules with the phase 2.1 type graph, and how much the check allocates. Later phases compare against these numbers and explain any regression.

## What was measured

Each run does two things in order:

1. Loads the solution with `ISolutionLoader` (MSBuild workspace, one compilation per project and target framework).
2. Checks the loaded projects with `IArchitectureValidator` against the repo's own `.architecture/rules.yaml`, resolved the way the CLI does.

The load time and the check time are timed separately with a `Stopwatch`. The check's allocations are the difference of `GC.GetTotalAllocatedBytes(true)` taken just before and just after `Validate`. Every loaded solution is disposed after its run. The tool is `tools/perf/Ariadne.NET.Perf`.

## Environment

| Item | Value |
| --- | --- |
| Date | 2026-11-04 |
| Commit | built on `6a1fd12`, with the perf tool added on top |
| CPU | Intel Core Ultra 7 255H |
| Cores | 12 logical |
| OS | Debian GNU/Linux 13 (trixie) |
| `dotnet --version` | 10.0.403 |
| Configuration | Release |

The numbers come from a shared VM. Other work was running on it, so expect noise. Compare medians, not single runs.

## Command

```
dotnet build src/Ariadne.NET.slnx -c Release
dotnet run --project tools/perf/Ariadne.NET.Perf -c Release --no-build -- src/Ariadne.NET.slnx --runs 5
```

Run it from the repository root, because the rules are read from `.architecture/rules.yaml` in the current directory.

## Results

Solution: `src/Ariadne.NET.slnx`, 12 projects, 199 types checked, 0 violations.

The repo's own rule pack reports no violations on its own solution. Phase 2.2 migrates the pack, so this count may change.

| Metric | Median of 5 runs |
| --- | --- |
| Load time | 3598 ms |
| Check time | 1063.4 ms |
| Check allocations | 313,239,504 bytes (about 299 MiB) |

Individual runs:

```
run 1: load 6493 ms, check 4138.6 ms, check allocated 342,785,496 bytes
run 2: load 3731 ms, check 1092.1 ms, check allocated 313,288,488 bytes
run 3: load 3598 ms, check 949.0 ms, check allocated 310,248,256 bytes
run 4: load 3388 ms, check 1063.4 ms, check allocated 313,133,152 bytes
run 5: load 3216 ms, check 951.3 ms, check allocated 313,239,504 bytes
```

Run 1 is slower than the rest because it pays for JIT compilation and cold caches. The median hides it, but a fresh process always sees it.
