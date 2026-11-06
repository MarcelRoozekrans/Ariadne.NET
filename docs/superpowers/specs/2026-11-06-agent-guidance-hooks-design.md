# Agent hooks and guidance delivery

**Status:** Draft for approval
**Date:** 2026-11-06

## Intent and why

Ariadne governs code structure through YAML rule packs, but today the agent
only meets those rules when someone runs `validate` or `lint`. Teams also want
to give AI agents prose standards (git and PR conventions, testing rules,
security basics) and workflow skills (TDD, bugfix), pushed into every
developer's agent from one central config repo.

The pain: such rules are normally baked into each project's `CLAUDE.md` and
`.claude/settings.json`, so a new project copies them and every copy drifts.
The goal is that a new project connects to Ariadne once and receives the rules
from then on, without them living in its own md and json files.

Ariadne already has the distribution half: `ariadne sync` resolves a git repo of
packs by semver tag, caches it offline in `.architecture/.cache/`, and
`extends` plus `locked` give an org-to-project override model. What is missing
is a delivery channel into the agent and, for the general guidance, content
types.

## Relationship to AI.Sentinel

AI.Sentinel (the author's other project) already ships a Claude Code hook
(`sentinel-hook`, Native AOT) with tool-call authorization, approval
workflows and an audit log. Findings from reading its source:

- The hook CLI has no extension seam. Its behaviour comes from `SENTINEL_HOOK_*`
  environment variables and one JSON approval config, and Native AOT rules out
  loading plugins at runtime.
- It fails closed on `PreToolUse`, as a security boundary should.
- Its allow/deny policies are C# classes; its only declarative rules are the
  approval config (tool to ordered rules with JSON-path and regex conditions).

Division of labour:

- **Ariadne** owns structure and delivery: structural rules in the agent's
  context, structural validation feedback, and distribution of packs, standards
  and skills.
- **AI.Sentinel** owns tool-call enforcement and security scanning. Ariadne
  builds no general guardrail engine and no tool-call policy language.
- A project that wants both has two hook entries. This is safe: Claude Code
  runs matching hooks in parallel, resolves `PreToolUse` decisions as
  `deny > defer > ask > allow`, and delivers every hook's `additionalContext`.
  (Checked against the Claude Code hooks documentation.)
- Ariadne can *deliver* a Sentinel config file to projects as an opaque synced
  attachment, so enforcement rules also stop being copied per project.
- Ariadne takes no package dependency on AI.Sentinel. It writes its own small
  hook wire types.

## Shape: one tool, two stages

One product, one install, one `ariadne hook` entry in a project's settings.
Inside the repo there is a hard boundary:

- `Ariadne.NET` (engine) and `Ariadne.NET.Abstractions` (schema) stay
  structural: layers, roles, rules, validator. They gain only a generic
  **pack attachment** concept (named files they carry and merge but do not
  interpret). Abstractions stays AOT-compatible with no new dependencies.
- `Ariadne.NET.Guidance` (new package, same repo, same release) interprets the
  attachments: standards, skills and opaque file delivery. It registers itself
  with the hook host. The engine never references it; a layer rule in
  `.architecture/rules.yaml` enforces that direction.
- A later split of Guidance into its own repo must be possible without
  touching the engine.

**Stage A, structural hooks (engine and the hook host):**
`ariadne hook` with `session-start`, `stop` and a small structural
`pre-tool-use`; `ariadne init --agent claude`.

**Stage B, general guidance (`Ariadne.NET.Guidance`):**
standards injection, skill materialisation, opaque file delivery, starter
content.

## Roadmap fit

Checked against `docs/planning/ROADMAP.md` and `STATE.md` (Milestone 2, phase
2.3 active):

- Stage A belongs in **Milestone 5, Agent surfaces**, as a new phase beside
  5.1 to 5.5. It is not in the roadmap yet; adding it is an owner decision.
- Stage B (Guidance) has no home in the roadmap. It widens Ariadne from
  structure to general agent guidance; the owner decides whether it is a new
  milestone or stays out of scope.
- Phase 5.5 (Claude Code plugin that teaches agents when to call Ariadne)
  overlaps with skill delivery. The plugin ships skills about *using Ariadne*
  through a marketplace; Guidance delivers *organisation-specific* skills and
  standards from a pack. Keep them separate and avoid shipping the same
  skill both ways.
- Phase 2.3 is changing the rule schema and loader that attachments extend, so
  none of this starts before 2.3 lands.

## Goals

- The agent meets the resolved structural rules at session start, and gets
  validator feedback, with each rule's reason, when it finishes a turn that
  edited `.cs` files.
- A new `.cs` file whose path maps to no known layer is questioned before it
  is written.
- Delivery uses the existing `ariadne sync` path: central repo, semver pin,
  private repos, offline cache.
- A project runs `ariadne init --agent claude` once; afterwards rule changes
  arrive through `ariadne sync` and the project's settings never change.
- Stage B: packs carry **standards** (markdown), **skills** (Agent Skills
  folders) and **opaque files** (such as a Sentinel approval config) with a
  target path, delivered the same way. `locked` works for all of them as it
  does for structural rules.

## Non-goals

- A general tool-call guardrail engine or policy language (AI.Sentinel's job).
- Compatibility with Rego or with any external policy service.
- Agents other than Claude Code in v1 (see Constraints).
- A hosted service or a new server. Delivery stays git plus local cache.
- Serving standards or skills through MCP: MCP cannot inject always-on context
  or register skills.
- Being a security boundary. Ariadne's hooks steer a cooperative agent.

## Anti-goals

- A hook that slows every tool call noticeably.
- A hook failure that blocks the agent from working.
- Rules silently applied from an unsynced or stale cache without saying so.
- Per-project copies of org rules (the drift this exists to remove).
- The engine growing knowledge of git, docker, testing philosophy or any other
  non-structural concern.
- Duplicating AI.Sentinel's hook host, audit or authorization features.

## Constraints

- Claude Code first. The hook event contract (`SessionStart`, `PreToolUse`,
  `PostToolUse`: JSON on stdin, JSON or exit code out) follows Claude Code's.
- Each injected context string is capped at 10,000 characters by Claude Code;
  beyond that the output is replaced by a file path and a 2,000 character
  preview. Anything the agent must always see stays under the cap.
- Skills are files. A `SessionStart` hook can make newly written skills
  available in the same session by returning `reloadSkills`.
- The user's own global rules win over shipped standards. Example: a standard
  requiring a `Co-Authored-By` trailer conflicts with the author's rule
  against Claude signatures. Shipped starter content must not contain such
  conflicts.
- Repo conventions apply: ZInject DI, analyzer rules, release-please commits.

## Approaches considered

1. **Hooks as delivery channel (chosen).** Live, no project-level copies of
   rules. Cost: a process start per tool call; settings.json holds fixed hook
   entries.
2. **MCP only.** Resources and prompts. Simple, but nothing is enforced or
   loaded automatically. Rejected.
3. **Scaffold generator.** `init` writes everything into the repo. Works
   offline but is the copy-and-drift problem. Rejected.

Packaging, chosen by the author after weighing three options: one tool with a
separate `Ariadne.NET.Guidance` package. Rejected: everything in the engine (blurs the
structural engine) and a second standalone tool (second install, repo,
release and hook entry).

Overlap with AI.Sentinel, chosen by the author after reading its source:
Ariadne owns structure and delivery, Sentinel owns enforcement. Rejected: a
hook that extends Sentinel's (impossible without a build-time dependency,
because of Native AOT) and keeping a full guardrail engine in Ariadne
(duplication).

## Design

### Pack attachments (Abstractions and engine)

A pack folder may carry attachment files next to its YAML, and `RulePack`
gains generic lists that the engine merges by name but does not interpret:

```yaml
standards: [git-and-pr, testing]          # files in standards/
skills: [tdd-workflow, bugfix-workflow]   # folders in skills/
files:                                    # opaque files with a target path
  - name: sentinel-approvals
    from: files/sentinel-approvals.json
    to: .sentinel/approvals.json
    locked: true
```

In stage A The engine only loads and merges these. `Ariadne.NET.Guidance` interprets
them in stage B.

### Merge semantics

Same model as today: `extends` order, later packs override earlier ones.
Standards, skills and files are keyed by name. A downstream pack may replace
an inherited item unless it is marked `locked`; an attempt is a merge error,
as amending a locked rule is today (merge reports errors only, collecting every
problem).

### Hook command

`ariadne hook <event>` reads the event JSON from stdin, resolves the project's
rules from `.architecture` (imports from the sync cache) and writes the result
to stdout.

- `session-start`: returns the resolved structural rules (layers, roles,
  naming) as `additionalContext`, plus pack versions in use, and warns if the
  cache is missing or stale ("run `ariadne sync`"). Stage B adds standards
  and, when it has written skills, `reloadSkills`.
- `pre-tool-use`: for a write of a new `.cs` file, uses the existing layer
  inference to check the path maps to a known layer; if not, returns `ask`
  with the reason. Everything else is allowed silently.
- `stop`: when the agent finishes a turn that edited `.cs` files, runs
  `ISolutionValidator` on the solution and returns the violations, each with
  its rule id and `because`, as feedback; silent when clean. It must not loop:
  it feeds back once per turn.

The validator is solution-wide: it loads the projects through
`MSBuildWorkspace` and checks one `TypeGraph` per compilation, with no
per-file mode. Running that after every single edit would cost seconds each
time, so the once-per-turn `stop` hook is the default trigger. A per-edit
`post-tool-use` is added only if measurement shows it is fast enough.

Failure policy: any internal error (unreadable cache, bad YAML, exception)
fails open with a one-line warning on stderr. The agent is never blocked by a
broken hook. A missing cache is reported, not silently ignored. This differs
deliberately from Sentinel, which is a security boundary and fails closed.

### Init and sync

- `ariadne init --agent claude` writes the hook entries into
  `.claude/settings.json` (merging with existing settings) and, with
  `--from <url>`, an `imports.yaml` entry for the org repo.
- Stage B: `ariadne sync` materialises synced skills into `.claude/skills/`
  and opaque files to their `to:` paths, each with a generated-file marker
  where the format allows one, and gitignores them.

### Starter content (stage B)

One example pack, separate from the built-ins: stack-neutral `git-and-pr`,
`testing`, `security-basics` and `code-quality` standards, and `tdd-workflow`
and `bugfix-workflow` skills using `dotnet`, with no Claude signature or
co-author lines. Written from scratch for this repo.

## Decided by the author

- Hooks as the delivery channel.
- Claude Code first, adapters later.
- Rules come from Ariadne rather than being baked into md and json.
- One tool, with the general guidance in a separate package in the same repo.
- Ariadne owns structure and delivery; AI.Sentinel owns tool-call enforcement.

## My calls (please check)

- Stage A is structural hooks; Guidance is stage B.
- Fail-open on internal errors.
- The structural `pre-tool-use` only returns `ask`, never `deny`, and only
  for a new `.cs` file outside every known layer.
- Standards (stage B) are injected at `SessionStart` within the 10,000
  character cap: an index plus the most important text, with full text left on
  disk for the agent to read on demand.
- Skills materialised under `.claude/skills/`, gitignored, regenerated by
  `ariadne sync`. Sync only deletes folders carrying the generated marker and
  never overwrites a user-authored skill of the same name (skips and warns).
- "Stale cache" is decided offline: the cached resolved tag no longer
  satisfies the pin's semver range, or the cache is absent. The hook never
  touches the network; `ariadne sync` does.
- `init --agent claude` is idempotent. It recognises its own entries by the
  `ariadne hook` command prefix, updates them in place and leaves the user's
  other hooks alone. The command is `ariadne hook <event>`, with `ariadne`
  installed on PATH as a dotnet tool.
- "The user's global rules win" is a rule for the starter content only. The
  engine does not enforce it.
- Native AOT for the hook entry point is a follow-up, only if measured startup
  is too slow. Note the hook host references `Ariadne.NET.Workspaces`, which
  uses MSBuild and is not AOT-friendly, so `stop` may need to run the
  validator out of process or in the CLI rather than the hook binary itself.
- Validation feedback runs on `stop`, once per turn, not after every edit
  (see Hook command).
- Attachments are an additive schema change. Merge conflicts on a locked
  attachment are merge errors, like an amended locked rule today, not
  warnings.
- No package dependency on AI.Sentinel; hook wire types are written locally.
- Starter content is original, not copied from another project.

## Left to the builder

- Exact JSON field names for each Claude Code hook event (verify against the
  current Claude Code hooks docs).
- Project layout for the hook host (new project vs. a mode of the CLI) and how
  Guidance registers with it.
- Truncation strategy for injected standards within the cap.
- Pack file discovery details and the merge error wording.
- How `PackYamlReader`'s closed key set learns the attachment keys, and
  whether that stays schema 2. Phase 2.3 is changing the same loader, so
  this work starts after 2.3 lands.
- Where the hook host lives (a new `Ariadne.NET.Hooks` project vs. a mode of
  `Ariadne.NET.Cli`), given the Workspaces/MSBuild constraint above.
- Whether the layer-inference check is precise enough for `ask` without noise;
  if not, drop `pre-tool-use` from stage A.
- Test strategy, following the existing test layout.
