# Phase 5.6 agent hook timings

This file records how long one `ariadne hook` call takes on this repo's own solution. It answers the open question in the agent hooks design: is validating after every edit, with a `PostToolUse` hook, fast enough? The answer is no. See the conclusion below.

## What was measured

The wall time of the whole process, from the shell starting `dotnet` to its exit, for the three events:

- `session-start` with `cwd` set to the repository root, so it reads `.architecture/rules.yaml` and renders the context.
- `pre-tool-use` for an `Edit` of `src/Ariadne.NET.Hooks/TextLimit.cs`, an existing file. It records the edit and finds no new file to question.
- `stop` with that one edit recorded for the session. It loads `src/Ariadne.NET.slnx` with MSBuild, validates it against the repo's rules and found no violations, so it printed nothing. The record is written again before each run, because `stop` clears it.

The time is taken with `date +%s%N` around `dotnet <dll> hook <event>`, with the hook input piped on stdin. Each event ran 7 times, in the order above, after the Release build. The runs are not discarded, so the first run of each event is included. Build time is not included.

## Environment

| Item | Value |
| --- | --- |
| Date | 2026-11-07 |
| Commit | `34a7729` plus the documentation changes of the commit that adds this file |
| CPU | Intel Core Ultra 7 255H |
| Cores | 12 logical |
| OS | Debian GNU/Linux 13 (trixie) |
| `dotnet --version` | 10.0.403 |
| Configuration | Release |

This is the shared VM of the earlier perf runs. The load average was about 2 before the runs and 2.7 after them, so other work was running. The numbers are indicative; compare orders of magnitude, not single milliseconds.

## Command

```bash
dotnet build -c Release src/Ariadne.NET.Cli
echo '{"session_id":"perf1","cwd":"<repo root>"}' | dotnet src/Ariadne.NET.Cli/bin/Release/net10.0/Ariadne.NET.Cli.dll hook session-start
echo '{"session_id":"perf1","cwd":"<repo root>","tool_name":"Edit","tool_input":{"file_path":"<repo root>/src/Ariadne.NET.Hooks/TextLimit.cs"}}' | dotnet <dll> hook pre-tool-use
echo '{"session_id":"perf2","cwd":"<repo root>","tool_name":"Edit","tool_input":{"file_path":"<repo root>/src/Ariadne.NET.Hooks/TextLimit.cs"}}' | dotnet <dll> hook pre-tool-use   # records the edit
echo '{"session_id":"perf2","cwd":"<repo root>"}' | dotnet <dll> hook stop
```

Each edit record is a small file in `ariadne-hooks-<user>/<session>/` under the temp folder (`/tmp` here).

## Results

| Event | Min | Median | Runs (ms) |
| --- | --- | --- | --- |
| `session-start` | 95 ms | 106 ms | 106, 104, 95, 115, 104, 116, 112 |
| `pre-tool-use` | 97 ms | 102 ms | 102, 103, 113, 102, 100, 97, 99 |
| `stop`, one recorded edit | 11,365 ms | 13,144 ms | 11893, 12547, 13288, 11365, 13144, 16800, 13998 |

## Conclusion

`session-start` and `pre-tool-use` cost about 0.1 s each, which is process start plus reading a small rules file. Both fit their timeouts (30 s and 10 s) by a wide margin, and the `pre-tool-use` call sits on every `Write`, `Edit` and `MultiEdit`, so it is cheap enough to stay there.

`stop` takes 11 to 17 s. The time was not broken down, but it loads the solution with MSBuild and then validates it, and the phase 2.1 baseline measured the load at about 3 s and the check at under 1 s on a similar run, so process start and MSBuild setup most likely account for a good part of it. That is acceptable once per turn, with a 300 s timeout. It is not acceptable after every edit: an agent that edits 10 files in a turn would wait about two minutes in total, and every call pays that cost again. A `PostToolUse` hook that validates per edit is therefore not worth building on this design. It would need a resident process that keeps the loaded solution, which is a different design. No code changed for this measurement.

The entry point itself is not the bottleneck: process start plus rule loading is about 0.1 s, so Native AOT for the `ariadne hook` entry point does not pay off and stays out unless a later measurement says otherwise.
