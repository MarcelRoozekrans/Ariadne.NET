# Phase 2.4 performance baseline

This file records the check on this repository's own solution after phase 2.4, which added `bannedPackages`, member-level `forbid` and `allowOnly` through `members`, and `uniqueNames`. It is measured the way [the phase 2.1 baseline](phase-2.1-baseline.md) describes (see its "What was measured" section) and compared with that baseline and with its "Phase 2.2" section.

## Environment

| Item | Value |
| --- | --- |
| Date | 2026-11-08 |
| Commit | `4b9090c` plus the documentation changes of the commit that adds this file |
| CPU | Intel Core Ultra 7 255H |
| Cores | 12 logical |
| OS | Debian GNU/Linux 13 (trixie) |
| `dotnet --version` | 10.0.403 |
| Configuration | Release |

This is the same shared VM as the earlier runs. The load average was 3.6 before the run and 2.8 after it, so other work was running and the timings are noisy. The allocation figures are the stable comparison. Compare medians, not single runs.

## Command

```
dotnet build src/Ariadne.NET.slnx -c Release
dotnet run --project tools/perf/Ariadne.NET.Perf -c Release --no-build -- src/Ariadne.NET.slnx --runs 5
```

Run from the repository root, because the rules are read from `.architecture/rules.yaml` in the current directory.

## Results

Solution: `src/Ariadne.NET.slnx`, 12 projects, 261 types checked, 0 violations. The checked rules are the repo's own `.architecture/rules.yaml`: 4 layers, 2 `forbid` rules, a `require` rule on the rule evaluators, a `require` rule on `IRulePackLoader`, a `uniqueNames` rule (Host against Core), a member `forbid` rule (`System.Console.*` from Core) and a `bannedPackages` rule (`Newtonsoft.Json`).

| Metric | Phase 2.1 | Phase 2.2, before caching | Phase 2.4 | Change vs 2.1 | Change vs 2.2 |
| --- | --- | --- | --- | --- | --- |
| Types checked | 199 | 224 | 261 | +62 (+31.2%) | +37 (+16.5%) |
| Load time (median of 5) | 3008 ms | 2737 ms | 3534 ms | +17% | +29% |
| Check time (median of 5) | 837.0 ms | 1012.7 ms | 1554.1 ms | +86% | +53% |
| Check allocations (median of 5) | 313,218,272 bytes (about 299 MiB) | 412,696,312 bytes (about 394 MiB) | 570,256,824 bytes (about 544 MiB) | +82% | +38% |
| Allocations per type | about 1.57 MB | about 1.84 MB | about 2.18 MB | +39% | +19% |
| Check time per type | about 4.2 ms | about 4.5 ms | about 5.95 ms | +42% | +32% |

Against the 2.2 figures after selector caching (1042.8 ms and 419,041,608 bytes for 226 types) the check is +49% in time and +36% in allocations.

Individual runs:

```
run 1: load 4183 ms, check 1677.4 ms, check allocated 570,288,312 bytes
run 2: load 3534 ms, check 1539.0 ms, check allocated 570,245,896 bytes
run 3: load 3764 ms, check 1601.9 ms, check allocated 570,256,824 bytes
run 4: load 3513 ms, check 1507.4 ms, check allocated 570,254,504 bytes
run 5: load 3207 ms, check 1554.1 ms, check allocated 570,258,400 bytes
```

The warm-up run is not listed. The load time does not depend on the rules; its difference is noise and a larger solution.

## What changed

The check regressed by more than 25% against the Phase 2.2 figures in both time and allocations. The causes I can name:

- The solution is larger. It has 261 types instead of 224 (+16.5%), because the phases since 2.2 added types and tests, and the type graph is built per type. That alone accounts for about 16 of the 38 points of allocation growth. Allocations per type still grew 19%, so the solution size is not the whole story.
- Phase 2.4 widened what the type graph records. Member edges are recorded when a `members` pattern exists in the pack, and the Core-layer `System.Console.*` rule makes one exist here. With the filter non-empty, every method, property, field, event and indexer use in a compilation can become a member edge, and constructors and initializers are bound for it. The graph build already binds every syntax node through the semantic model (see the 2.2 section), so the extra edges and the extra bound constructor and initializer nodes are the likely cause of the growth per type.
- `bannedPackages` and `uniqueNames` are cheap passes. Neither walks syntax or binds anything: one looks at package references and the other compares type names.

### Attribution experiment

To separate the member-edge cost, I ran the same command from a temporary directory holding a copy of the pack without the `ARIADNE-CORE-NO-CONSOLE` rule (the real pack was not touched, and the copy was deleted). With no `members` pattern, the graph records no member edges.

| Pack | Check time (median of 5) | Check allocations (median of 5) |
| --- | --- | --- |
| Repo pack (with the `System.Console.*` rule) | 1554.1 ms | 570,256,824 bytes |
| Copy without the `System.Console.*` rule | 1539.6 ms | 563,562,096 bytes |

The member rule costs about 6.7 MB, or 1.2%, of the check allocations, and about 1% of the time, which is within the noise. So the member-edge recording is not what makes the check larger than in phase 2.2. The remaining growth sits in the type graph build that happens with or without the rule: the 16.5% larger solution, and whatever changed since phase 2.2 in the graph build for every pack. I did not profile the graph build, and I did not re-measure the 2.2 commit on today's solution, so I cannot split the rest between the larger solution and per-type work.

The check is still not a hot loop in normal use: it runs once per validation, next to a load of 3 to 4 seconds.

## Attribution

The regression over Phase 2.2 has two parts. Phase 2.3 recorded no perf figure, so I measured its head: commit `248c0e9`, the merge base of this branch with `ariadne/main` (the current `main` has moved on to phase 5.6 and is not comparable). It was built and measured the same way in a temporary worktree outside the repository, with its own `.architecture/rules.yaml`, on the same VM (load average 2.6 before the run). The worktree was removed afterwards.

Phase 2.3 head: 12 projects, 235 types, 0 violations.

```
run 1: load 4273 ms, check 1333.7 ms, check allocated 461,561,536 bytes
run 2: load 3331 ms, check 1266.6 ms, check allocated 461,505,920 bytes
run 3: load 3130 ms, check 1273.9 ms, check allocated 461,488,600 bytes
run 4: load 3362 ms, check 1264.8 ms, check allocated 461,495,472 bytes
run 5: load 3397 ms, check 1236.3 ms, check allocated 456,906,656 bytes
median (5 runs): load 3362 ms, check 1266.6 ms, check allocated 461,495,472 bytes
```

| Metric | Phase 2.2 | Phase 2.3 head | Phase 2.4 | 2.3 vs 2.2 | 2.4 vs 2.3 | 2.4 vs 2.2 |
| --- | --- | --- | --- | --- | --- | --- |
| Types checked | 224 | 235 | 261 | +4.9% | +11.1% | +16.5% |
| Check time | 1012.7 ms | 1266.6 ms | 1554.1 ms | +25% | +23% | +53% |
| Check allocations | 412,696,312 bytes | 461,495,472 bytes | 570,256,824 bytes | +12% | +24% | +38% |
| Allocations per type | about 1.84 MB | about 1.96 MB | about 2.18 MB | +7% | +11% | +19% |
| Check time per type | about 4.5 ms | about 5.4 ms | about 5.95 ms | +19% | +10% | +32% |

By allocations, which are the stable figure, about a third of the regression over 2.2 is phase 2.3 and two thirds is phase 2.4. Of the 2.4 part, 11 points are the larger solution (261 types against 235) and about 11 points are cost per type. By time, the split is about half and half, but the timings are noisy.

### Experiment: every new feature off

To see whether the 2.4 cost per type comes from the new rules or from the changed walker and validator, I ran the 2.4 build from a temporary directory with a copy of the pack that has the 4 layers and the first 4 rules only: no `members`, no `bannedPackages`, no `uniqueNames`. The copy was deleted afterwards and the real pack was not touched.

| Pack | Check time (median of 5) | Check allocations (median of 5) |
| --- | --- | --- |
| Repo pack | 1554.1 ms | 570,256,824 bytes |
| No `members`, `bannedPackages` or `uniqueNames` rules | 1374.5 ms | 563,343,792 bytes |

Allocations per type with the new rules off are about 2.16 MB, 10% above the 2.3 head. So the three new rules together cost about 1.2% of the allocations, and nearly all of the 2.4 growth per type is there with every new feature off. The time difference of 180 ms is larger than the allocation difference suggests, but the two runs were minutes apart on a loaded VM, so I do not read it as the cost of the rules.

My best guess for the cause is the unconditional change to the type graph build in phase 2.4, such as the walker's resolve and edge recording path or the constructor and initializer binding, which runs whether or not the pack has a `members` pattern. Part of it may also be the mix of types in the added code, because the 2.3 and 2.4 packs and solutions differ and I did not profile the build. I did not change production code. Phase 2.4 alone adds about 11% to allocations and time per type over the 2.3 head, just over the 10% bar.
