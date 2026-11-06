# Agent guidance and guardrails via hooks

**Status:** Draft for approval
**Date:** 2026-11-06

## Intent and why

Ariadne governs code structure today through YAML rule packs. Teams also need
to give AI agents prose standards (git and PR conventions, testing rules,
security basics), workflow skills (TDD, bugfix) and tool-call guardrails
(never `git add -A`, never `terraform apply`), all pushed into every
developer's agent from one central config repo.

The pain being solved: such rules are normally baked into each project's
`CLAUDE.md` and `.claude/settings.json`, so a new project has to copy them and
every copy drifts. The goal is that a new project connects to Ariadne once and
receives the rules from then on, without the rules living in its own md and
json files.

Ariadne already has the distribution half: `arch sync` resolves a git repo of
packs by semver tag, caches it offline in `.architecture/.cache/`, and
`extends` plus `locked` give an org-to-project override model. What is missing
is the content types and a delivery channel into the agent.

## Goals

- A pack can carry **standards** (markdown), **skills** (Agent Skills format
  folders) and **guardrails** (declarative tool-call rules), next to the
  existing structural rules.
- Delivery uses the existing `arch sync` path: central repo, semver pin,
  private repos, offline cache.
- One command, `archgov hook <event>`, is the delivery channel into Claude
  Code. A project runs `archgov init --agent claude` once; afterwards rule
  changes arrive through `arch sync` and the project's settings never change.
- `locked` works for guardrails as it does for structural rules: an org
  guardrail cannot be removed or weakened downstream.
- Reuse the existing source validator so post-edit feedback reports layer and
  dependency violations.

## Non-goals

- Compatibility with Rego or with any external policy service.
- Agents other than Claude Code in v1 (see Constraints).
- A hosted service or a new server. Delivery stays git plus local cache.
- Serving standards or skills through MCP. Not enough: MCP cannot inject
  always-on context or register skills.
- Guardrails as a security boundary. They steer a cooperative agent; they do
  not stop a determined one or a human.

## Anti-goals

- A hook that slows every tool call noticeably.
- A hook failure that blocks the agent from working.
- Rules silently applied from an unsynced or stale cache without saying so.
- Per-project copies of org rules (the drift this exists to remove).

## Constraints

- Claude Code first. The hook event contract (`SessionStart`, `PreToolUse`,
  `PostToolUse`: JSON on stdin, JSON or exit code out) follows Claude Code's.
  The guardrail model itself stays agent-agnostic so adapters can follow.
- Hooks cannot register skills, so skills are materialised as files.
- The user's own global rules win over shipped standards. Example: a
  standard requiring a `Co-Authored-By` trailer conflicts with the author's
  rule against Claude signatures. Shipped starter content must not contain
  such conflicts.
- Repo conventions apply: ZInject DI, analyzer rules, release-please commits.

## Approaches considered

1. **Hooks as delivery channel (chosen).** `SessionStart` injects standards,
   `PreToolUse` enforces guardrails, `PostToolUse` runs the validator. Skills
   are synced as files. Live, enforced, no project-level copies of rules.
   Cost: a process start per tool call; settings.json holds three fixed hook
   entries.
2. **MCP only.** Serve standards and skills as MCP resources and prompts.
   Simple, but nothing is enforced, nothing is loaded automatically, and no
   guardrails. Rejected.
3. **Scaffold generator.** `init` writes everything into the repo. Works
   offline, but it is the copy-and-drift problem. Rejected.

## Design

### Pack additions

A pack folder gains, next to its YAML:

```
standards/<name>.md          prose standards, optional frontmatter description
skills/<name>/SKILL.md       Agent Skills format
```

and `RulePack` gains two lists:

```yaml
standards: [git-and-pr, testing]       # names of files in standards/
skills: [tdd-workflow, bugfix-workflow]
guardrails:
  - id: GIT-001
    tool: Bash                         # Claude Code tool name
    match: '^git add (-A|--all|\.)(\s|$)'   # regex on the command
    decision: deny                     # allow | ask | deny
    message: "Stage explicit paths."
    locked: true
```

For file tools the match is a glob on `file_path`; for `Bash` it is a regex on
`command`.

### Merge semantics

Same model as today: `extends` order, later packs override earlier ones.
Standards and skills are add-only by name (a downstream pack can add or
replace its own, not remove an inherited one that is locked). Guardrails merge
by `id`; a `locked: true` guardrail cannot be overridden or removed, and an
attempt produces a `MergeWarning`, as locked code feature rules do now.
Evaluation: most severe decision wins (deny over ask over allow); no match
means allow.

### Hook command

`archgov hook <event>` reads the event JSON from stdin, resolves the project's
rules from `.architecture` (imports from the sync cache), and writes the
result to stdout.

- `session-start`: prints the resolved standards as additional context.
  Also states the pack versions in use and warns if the cache is missing or
  older than the pin ("run `arch sync`").
- `pre-tool-use`: evaluates guardrails, returns the decision and message.
- `post-tool-use`: for an edit or write of a `.cs` file, runs the source
  validator on that file and returns violations as feedback to the agent;
  silent when clean.

Failure policy: any internal error (unreadable cache, bad YAML, exception)
fails open with a one-line warning on stderr. The agent is never blocked by a
broken hook. A missing cache is reported, not silently ignored.

### Init and sync

- `archgov init --agent claude` writes three hook entries into
  `.claude/settings.json` (merging with existing settings) and an
  `imports.yaml` entry for the org repo if given with `--from <url>`.
- `arch sync` additionally materialises synced skills into `.claude/skills/`,
  each with a generated-file marker, and gitignores that folder. Skills are
  therefore not committed in consuming projects.

### Starter content

Ship one example pack (separate from the built-ins): stack-neutral
`git-and-pr`, `testing`, `security-basics` and `code-quality` standards,
`tdd-workflow` and `bugfix-workflow` skills using `dotnet`, no Claude
signature or co-author lines, and a small guardrail set covering dangerous
commands, git, gh and docker. Written from scratch for this repo.

## Decided by the author

- Delivery approach: hooks as the channel (above).
- Claude Code first, adapters later.
- Rules should come from Ariadne rather than being baked into md and json.

## My calls (please check)

- Standards are injected at `SessionStart` as context, with a size cap rather
  than the full text when large.
- Fail-open on internal errors.
- Skills materialised under `.claude/skills/` and gitignored, regenerated by
  `arch sync`.
- Guardrail matching: regex for Bash commands, glob for file paths.
- Starter content is original, not copied from another project.
- Native AOT for the hook entry point is a follow-up, only if measured
  startup is too slow.
- Guardrail `tool` is a single name, a list, or `*`. File globs are matched
  against project-relative, forward-slash paths and read the tool's path
  field (`file_path`, or `notebook_path` for notebooks). Tools without a path
  are matched on `tool` alone.
- `allow` means "no opinion": the hook emits nothing, so it never bypasses the
  user's own permission prompts. Only `ask` and `deny` produce output.
- Standards and skills are keyed by name. A downstream pack may replace an
  inherited item unless it is marked `locked`, which works as for guardrails.
  Locked-override attempts become a `MergeWarning`, shown in `arch sync`
  output and on stderr by the hook.
- "Stale cache" is decided offline: the cached resolved tag no longer
  satisfies the pin's semver range, or the cache is absent. The hook never
  touches the network; `arch sync` does.
- `init --agent claude` is idempotent. It recognises its own entries by the
  `archgov hook` command prefix, updates them in place and leaves the user's
  other hooks alone. The command is `archgov hook <event>`, with `archgov`
  installed on PATH as a dotnet tool.
- Sync only deletes skill folders that carry the generated marker. A
  user-authored skill with the same name is never overwritten; sync skips it
  and warns.
- "The user's global rules win" is a rule for the starter content only. The
  engine does not enforce it.
- The post-edit validator may need a per-file mode if the whole-project run
  is too slow; the builder checks this against `SourceValidator`.

## Left to the builder

- Exact JSON field names for each Claude Code hook event (verify against the
  current Claude Code hooks docs).
- Project layout for the hook host (new project vs. a mode of the CLI).
- Size cap value and truncation strategy for injected standards.
- Pack file discovery details and the `MergeWarning` wording.
- Test strategy, following the existing test layout.
