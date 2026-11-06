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

Ariadne already has the distribution half: `arch sync` resolves a git repo of
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

## Shape: one tool, two milestones

One product, one install, one `archgov hook` entry in a project's settings.
Inside the repo there is a hard boundary:

- `ArchGov.Core` stays structural: layers, roles, naming, validator. It gains
  only a generic **pack attachment** concept (named files it carries and merges
  but does not interpret).
- `ArchGov.Guidance` (new package, same repo, same release) interprets the
  attachments: standards, skills and opaque file delivery. It registers itself
  with the hook host. Core never references it; a layer rule in
  `.architecture/rules.yaml` enforces that direction.
- A later split of Guidance into its own repo must be possible without
  touching Core.

**Milestone 1, structural hooks (Core and the hook host):**
`archgov hook` with `session-start`, `post-tool-use` and a small structural
`pre-tool-use`; `archgov init --agent claude`.

**Milestone 2, general guidance (`ArchGov.Guidance`):**
standards injection, skill materialisation, opaque file delivery, starter
content.

## Goals

- The agent meets the resolved structural rules at session start, and gets
  validator feedback right after it edits a `.cs` file.
- A new `.cs` file whose path maps to no known layer is questioned before it
  is written.
- Delivery uses the existing `arch sync` path: central repo, semver pin,
  private repos, offline cache.
- A project runs `archgov init --agent claude` once; afterwards rule changes
  arrive through `arch sync` and the project's settings never change.
- Milestone 2: packs carry **standards** (markdown), **skills** (Agent Skills
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
- Core growing knowledge of git, docker, testing philosophy or any other
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
separate `ArchGov.Guidance` package. Rejected: everything in Core (blurs the
structural engine) and a second standalone tool (second install, repo,
release and hook entry).

Overlap with AI.Sentinel, chosen by the author after reading its source:
Ariadne owns structure and delivery, Sentinel owns enforcement. Rejected: a
hook that extends Sentinel's (impossible without a build-time dependency,
because of Native AOT) and keeping a full guardrail engine in Ariadne
(duplication).

## Design

### Pack attachments (Core)

A pack folder may carry attachment files next to its YAML, and `RulePack`
gains generic lists that Core merges by name but does not interpret:

```yaml
standards: [git-and-pr, testing]          # files in standards/
skills: [tdd-workflow, bugfix-workflow]   # folders in skills/
files:                                    # opaque files with a target path
  - name: sentinel-approvals
    from: files/sentinel-approvals.json
    to: .sentinel/approvals.json
    locked: true
```

In milestone 1 Core only loads and merges these. `ArchGov.Guidance` interprets
them in milestone 2.

### Merge semantics

Same model as today: `extends` order, later packs override earlier ones.
Standards, skills and files are keyed by name. A downstream pack may replace
an inherited item unless it is marked `locked`; an attempt becomes a
`MergeWarning`, shown in `arch sync` output and on stderr by the hook.

### Hook command

`archgov hook <event>` reads the event JSON from stdin, resolves the project's
rules from `.architecture` (imports from the sync cache) and writes the result
to stdout.

- `session-start`: returns the resolved structural rules (layers, roles,
  naming) as `additionalContext`, plus pack versions in use, and warns if the
  cache is missing or stale ("run `arch sync`"). Milestone 2 adds standards
  and, when it has written skills, `reloadSkills`.
- `pre-tool-use`: for a write of a new `.cs` file, uses the existing layer
  inference to check the path maps to a known layer; if not, returns `ask`
  with the reason. Everything else is allowed silently.
- `post-tool-use`: for an edit or write of a `.cs` file, runs the source
  validator and returns violations as feedback; silent when clean.

Failure policy: any internal error (unreadable cache, bad YAML, exception)
fails open with a one-line warning on stderr. The agent is never blocked by a
broken hook. A missing cache is reported, not silently ignored. This differs
deliberately from Sentinel, which is a security boundary and fails closed.

### Init and sync

- `archgov init --agent claude` writes the hook entries into
  `.claude/settings.json` (merging with existing settings) and, with
  `--from <url>`, an `imports.yaml` entry for the org repo.
- Milestone 2: `arch sync` materialises synced skills into `.claude/skills/`
  and opaque files to their `to:` paths, each with a generated-file marker
  where the format allows one, and gitignores them.

### Starter content (milestone 2)

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

- Milestone 1 is structural hooks; Guidance is milestone 2.
- Fail-open on internal errors.
- The structural `pre-tool-use` only returns `ask`, never `deny`, and only
  for a new `.cs` file outside every known layer.
- Standards (milestone 2) are injected at `SessionStart` within the 10,000
  character cap: an index plus the most important text, with full text left on
  disk for the agent to read on demand.
- Skills materialised under `.claude/skills/`, gitignored, regenerated by
  `arch sync`. Sync only deletes folders carrying the generated marker and
  never overwrites a user-authored skill of the same name (skips and warns).
- "Stale cache" is decided offline: the cached resolved tag no longer
  satisfies the pin's semver range, or the cache is absent. The hook never
  touches the network; `arch sync` does.
- `init --agent claude` is idempotent. It recognises its own entries by the
  `archgov hook` command prefix, updates them in place and leaves the user's
  other hooks alone. The command is `archgov hook <event>`, with `archgov`
  installed on PATH as a dotnet tool.
- "The user's global rules win" is a rule for the starter content only. The
  engine does not enforce it.
- Native AOT for the hook entry point is a follow-up, only if measured startup
  is too slow. The post-edit validator may need a per-file mode if the
  whole-project run is too slow.
- No package dependency on AI.Sentinel; hook wire types are written locally.
- Starter content is original, not copied from another project.

## Left to the builder

- Exact JSON field names for each Claude Code hook event (verify against the
  current Claude Code hooks docs).
- Project layout for the hook host (new project vs. a mode of the CLI) and how
  Guidance registers with it.
- Truncation strategy for injected standards within the cap.
- Pack file discovery details and the `MergeWarning` wording.
- Whether the layer-inference check is precise enough for `ask` without noise;
  if not, drop `pre-tool-use` from milestone 1.
- Test strategy, following the existing test layout.
