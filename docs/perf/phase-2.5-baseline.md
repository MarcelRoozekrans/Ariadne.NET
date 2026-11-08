# Phase 2.5 performance baseline

This file records the check on this repository's own solution after phase 2.5, which added vacuity detection: a rule, component role or layer that selects no type fails the run with `VacuousRule` or `VacuousLayer`. It is measured the way [the phase 2.1 baseline](phase-2.1-baseline.md) describes (see its "What was measured" section) and compared with [the phase 2.4 baseline](phase-2.4-baseline.md).

## Environment

| Item | Value |
| --- | --- |
| Date | 2026-11-08 |
| Commit | `a76798e` plus the documentation changes of the commit that adds this file |
| CPU | Intel Core Ultra 7 255H |
| Cores | 12 logical |
| OS | Debian GNU/Linux 13 (trixie) |
| `dotnet --version` | 10.0.403 |
| Configuration | Release |

This is the same shared VM as the earlier runs. The load average was 5.1 before the run and 3.5 after it, so other work was running and the timings are noisy. The allocation figures are the stable comparison.

## Command

```
dotnet build src/Ariadne.NET.slnx -c Release
dotnet run --project tools/perf/Ariadne.NET.Perf -c Release --no-build -- src/Ariadne.NET.slnx --runs 5
```

Run from the repository root, because the rules are read from `.architecture/rules.yaml` in the current directory.

## Results

Solution: `src/Ariadne.NET.slnx`, 14 projects, 326 types checked, 0 violations. The repo's own pack has no vacuous element.

```
run 1: load 3449 ms, check 1909.8 ms, check allocated 758,621,352 bytes
run 2: load 3600 ms, check 1870.6 ms, check allocated 758,526,088 bytes
run 3: load 3085 ms, check 1829.5 ms, check allocated 758,536,640 bytes
run 4: load 3067 ms, check 1782.4 ms, check allocated 758,524,680 bytes
run 5: load 3008 ms, check 1786.5 ms, check allocated 758,533,984 bytes
median (5 runs): load 3085 ms, check 1829.5 ms, check allocated 758,533,984 bytes
```

| Metric | Phase 2.4, after the fixes | Phase 2.5 | Change |
| --- | --- | --- | --- |
| Projects | 12 | 14 | +2 |
| Types checked | 262 | 326 | +64 (+24%) |
| Load time (median of 5) | 2836 ms | 3085 ms | +9% |
| Check time (median of 5) | 1402.9 ms | 1829.5 ms | +30% |
| Check allocations (median of 5) | 574,905,784 bytes | 758,533,984 bytes | +32% |
| Allocations per type | about 2.194 MB | about 2.327 MB | +6% |

The solution grew by 24% in types, which explains most of the change in the totals. The 2.4 figures were also taken on a quieter VM (load average 0.8), so the time comparison is weak.

## Cost of the vacuity pass

To separate the pass from the growth of the solution, I built the commit before the default changed (`9ee6b09`, the parent of `35ae809`) in a temporary worktree outside the repository and measured it the same way on the same VM, one run at a time. The worktree was removed afterwards.

| Tree | Types | Check time (median of 5) | Check allocations (median of 5) |
| --- | --- | --- | --- |
| `9ee6b09`, before the default changed | 325 | 1796.3 ms | 741,315,176 bytes |
| Phase 2.5 head | 326 | 1829.5 ms | 758,533,984 bytes |

The head allocates 17.2 MB, or 2.3%, more than the commit before it, for one more type. That is the cost of the vacuity pass, which re-runs the layer and selector matching over the source types and edges of every graph. The time difference of 33 ms (1.8%) is within the noise of this VM. This is within the noise that the earlier phases accepted, and the pass runs once per validation next to a load of 3 seconds.
