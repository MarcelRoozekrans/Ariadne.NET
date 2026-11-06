# Agent hooks and guidance delivery

**Status:** Draft for approval
**Date:** 2026-11-06

## Intent and why

Ariadne governs code structure through YAML rule packs, but today the agent
only meets those rules when someone runs `validate` or `lint`. Teams also want
to give AI agents prose standards (git and PR conventions, testing rules,
security basics), workflow skills (TDD, bugfix) and tool-call guardrails
(never `git add -A`, never `terraform apply`), all pushed into every
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

## Shape: one tool, two milestones

One product, one install, one `archgov hook` entry in a project's settings.
Inside the repo there is a hard boundary:

- `ArchGov.Core` stays structural: layers, roles, naming, validator. It gains
  only a generic **pack attachment** concept (named files and guardrail
  entries it carries and merges but does not interpret).
- `ArchGov.Guidance` (new package, same repo, same release) interprets the
  attachments: standards, skills and general guardrails. It registers itself
  with the hook host. Core never references it; a layer rule in
  `.architecture/rules.yaml` enforces that direction.
- A later split of Guidance into its own repo must be possible without
  touching Core.

**Milestone 1, structural hooks (Core and the hook host):**
`archgov hook` with `session-start`, `post-tool-use` and `pre-tool-use`;
`archgov init --agent claude`; structural guardrails.

**Milestone 2, general guidance (`ArchGov.Guidance`):**
standards injection, skill materialisation, general guardrails, starter
content.

## Goals

- The agent meets the resolved structural rules at session start, and gets
  validator feedback right after it edits a `.cs` file.
- Structural guardrails: block or question a file write whose path breaks the
  layer or naming convention.
- Delivery uses the existing `arch sync` path: central repo, semver pin,
  private repos, offline cache.
- A project runs `archgov init --agent claude` once; afterwards rule changes
  arrive through `arch sync` and the project's settings never change.
- `locked` works for guardrails as it does for structural rules.
- Milestone 2: packs carry **standards** (markdown), **skills** (Agent Skills
  folders) and **general guardrails** (tool-call rules such as for git or
  docker), delivered the same way.

## Non-goals

- Compatibility with Rego or with any external policy service.
- Agents other than Claude Code in v1 (see Constraints).
- A hosted service or a new server. Delivery stays git plus local cache.
- Serving standards or skills through MCP: MCP cannot inject always-on context
  or register skills.
- Guardrails as a security boundary. They steer a cooperative agent; they do
  not stop a determined one or a human.

## Anti-goals

- A hook that slows every tool call noticeably.
- A hook failure that blocks the agent from working.
- Rules silently applied from an unsynced or stale cache without saying so.
- Per-project copies of org rules (the drift this exists to remove).
- Core growing knowledge of git, docker, testing philosophy or any other
  non-structural concern.

## Constraints

- Claude Code first. The hook event contract (`SessionStart`, `PreToolUse`,
  `PostToolUse`: JSON on stdin, JSON or exit code out) follows Claude Code's.
  The guardrail model stays agent-agnostic so adapters can follow.
- Hooks cannot register skills, so skills are materialised as files
  (milestone 2).
- The user's own global rules win over shipped standards. Example: a standard
  requiring a `Co-Authored-By` trailer conflicts with the author's rule
  against Claude signatures. Shipped starter content must not contain such
  conflicts.
- Repo conventions apply: ZInject DI, analyzer rules, release-please commits.

## Approaches considered

1. **Hooks as delivery channel (chosen).** Live, enforced, no project-level
   copies of rules. Cost: a process start per tool call; settings.json holds
   fixed hook entries.
2. **MCP only.** Resources and prompts. Simple, but nothing is enforced or
   loaded automatically, and no guardrails. Rejected.
3. **Scaffold generator.** `init` writes everything into the repo. Works
   offline but is the copy-and-drift problem. Rejected.

Scope of the packaging, chosen by the author after weighing three options:
one tool with a separate `ArchGov.Guidance` package. Rejected: everything in
Core (blurs the structural engine) and a second standalone tool (second
install, repo, release and hook entry per project).

## Design

### Pack attachments (Core)

A pack folder may carry attachment files next to its YAML, and `RulePack`
gains generic lists that Core merges by name or id but does not interpret:

```yaml
standards: [git-and-pr, testing]          # files in standards/
skills: [tdd-workflow, bugfix-workflow]   # folders in skills/
guardrails:
  - id: LAYER-001
    tool: Write
    match: 'src/*.Domain/**'              # glob on the project-relative path
    decision: ask                         # allow | ask | deny
    message: "Domain files are generated through archgov generate."
    locked: true
```

Structural guardrails (milestone 1) are evaluated by Core's own evaluator.
Standards and skills are only carried in milestone 1; `ArchGov.Guidance`
interprets them in milestone 2.

### Merge semantics

Same model as today: `extends` order, later packs override earlier ones.
Standards, skills and guardrails are keyed by name or id. A downstream pack may
replace an inherited item unless it is marked `locked`; an attempt becomes a
`MergeWarning`, shown in `arch sync` output and on stderr by the hook.
Guardrail evaluation: most severe decision wins (deny over ask over allow); no
match means allow.

### Hook command

`archgov hook <event>` reads the event JSON from stdin, resolves the project's
rules from `.architecture` (imports from the sync cache) and writes the result
to stdout.

- `session-start`: prints the resolved structural rules (layers, roles,
  naming) as additional context, plus pack versions in use, and warns if the
  cache is missing or stale ("run `arch sync`"). Milestone 2 adds the
  standards.
- `pre-tool-use`: evaluates guardrails and returns the decision and message.
- `post-tool-use`: for an edit or write of a `.cs` file, runs the source
  validator and returns violations as feedback; silent when clean.

Failure policy: any internal error (unreadable cache, bad YAML, exception)
fails open with a one-line warning on stderr. The agent is never blocked by a
broken hook. A missing cache is reported, not silently ignored.

### Init and sync

- `archgov init --agent claude` writes the hook entries into
  `.claude/settings.json` (merging with existing settings) and, with
  `--from <url>`, an `imports.yaml` entry for the org repo.
- Milestone 2: `arch sync` also materialises synced skills into
  `.claude/skills/`, each with a generated-file marker, and gitignores that
  folder.

### Starter content (milestone 2)

One example pack, separate from the built-ins: stack-neutral `git-and-pr`,
`testing`, `security-basics` and `code-quality` standards, `tdd-workflow` and
`bugfix-workflow` skills using `dotnet`, no Claude signature or co-author
lines, and a small guardrail set covering dangerous commands, git, gh and
docker. Written from scratch for this repo.

## Decided by the author

- Hooks as the delivery channel.
- Claude Code first, adapters later.
- Rules come from Ariadne rather than being baked into md and json.
- One tool, with the general guidance in a separate package in the same repo.

## My calls (please check)

- Milestone 1 is structural hooks; Guidance is milestone 2.
- Fail-open on internal errors.
- Standards (milestone 2) are injected at `SessionStart`, with a size cap.
- Skills materialised under `.claude/skills/`, gitignored, regenerated by
  `arch sync`; sync only deletes folders carrying the generated marker and
  never overwrites a user-authored skill of the same name (skips and warns).
- Guardrail `tool` is a single name, a list, or `*`. Bash matches by regex on
  the command; file tools by glob on the project-relative, forward-slash path
  (`file_path`, or `notebook_path` for notebooks). Tools without a path match
  on `tool` alone.
- `allow` means "no opinion": the hook emits nothing, so it never bypasses the
  user's own permission prompts. Only `ask` and `deny` produce output.
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
- Starter content is original, not copied from another project.

## Left to the builder

- Exact JSON field names for each Claude Code hook event (verify against the
  current Claude Code hooks docs).
- Project layout for the hook host (new project vs. a mode of the CLI) and how
  Guidance registers with it.
- Size cap value and truncation strategy for injected standards.
- Pack file discovery details and the `MergeWarning` wording.
- Test strategy, following the existing test layout.
