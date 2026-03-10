# Git-Based Sync Design

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:writing-plans to create the implementation plan from this design.

**Goal:** Allow teams to share rule packs via git repositories (including private repos), with local caching and semver-based version resolution via git tags.

**Architecture:** A standalone `arch sync` CLI command reads `.architecture/imports.yaml`, resolves versions against git tags, and caches pack files locally. The system shells out to `git` CLI for all git operations, inheriting the user's existing authentication (SSH keys, credential managers).

**Tech Stack:** System.CommandLine 3.0-preview, YamlDotNet, git CLI, System.Diagnostics.Process

---

## Configuration Schema

Rule pack imports are declared in `.architecture/imports.yaml` at the solution root:

```yaml
imports:
  - name: company-standards
    type: git
    url: git@github.com:acme/architecture-packs.git
    path: packs/clean-architecture   # subfolder within repo (optional, defaults to root)
    version: "^2.0"                  # semver range resolved against git tags

  - name: team-conventions
    type: git
    url: https://github.com/acme/team-rules.git
    version: "1.3.0"                 # exact tag
```

- `url` supports both SSH (`git@...`) and HTTPS — private repos work via the user's existing git credential setup
- `version` supports exact tags (`1.3.0`) and semver ranges (`^2.0`, `~1.3`, `>=1.0 <2.0`)
- `path` is optional — when a git repo contains multiple packs in subdirectories
- `name` is the local alias used to reference the pack

## Cache Strategy

Synced packs are cached locally in `.architecture/.cache/`:

```
.architecture/
├── imports.yaml
└── .cache/
    └── git/
        ├── company-standards/
        │   ├── .git-info.json          # { url, resolvedTag, commitSha, syncedAt }
        │   └── <pack files>
        └── team-conventions/
            ├── .git-info.json
            └── <pack files>
```

- `arch sync` — checks each import, skips if cached version satisfies the version constraint. Fetches only when needed.
- `arch sync --no-cache` — ignores cache, does a fresh clone/fetch for all imports.
- Cache is gitignored (`.architecture/.cache/` added to `.gitignore`).
- `arch validate` / `arch lint` resolve imported packs from cache — if cache is missing, they emit a clear error: "Run `arch sync` first."

## Version Resolution

Git tags are treated as semver versions:

1. `arch sync` runs `git ls-remote --tags <url>` to list all tags (works without cloning).
2. Tags matching semver pattern (e.g., `v1.0.0`, `2.3.1`) are collected.
3. The best match for the version constraint is selected (e.g., `^2.0` picks highest `2.x`).
4. A shallow clone fetches only that tag's content at the specified `path`.

## Git Sync Provider

```csharp
public interface IGitSyncProvider
{
    Task<SyncResult> SyncAsync(ImportDefinition import, string cacheDir, bool noCache, CancellationToken ct);
    Task<IReadOnlyList<string>> ListTagsAsync(string url, CancellationToken ct);
    Task<string?> ResolveVersionAsync(string url, string versionRange, CancellationToken ct);
}
```

Implementation shells out to `git` CLI rather than using libgit2:
- Private repo support for free — uses whatever auth the user already has
- No native library dependencies
- `git ls-remote --tags` for version listing
- `git clone --depth 1 --branch <tag>` for fetching
- `git sparse-checkout` when `path` is specified

## CLI Command

```
arch sync                    # sync all imports from .architecture/imports.yaml
arch sync --no-cache         # force fresh sync, ignore cached versions
arch sync --name foo         # sync only the import named "foo"
```

Output:
```
Syncing 2 imports...
  company-standards  v2.4.1 (cached)
  team-conventions   v1.3.0 (fetched)
Done. 2 packs synced.
```

## Integration with RulePackResolver

- `imports.yaml` entries become resolvable via `import://<name>` protocol
- Resolver reads from `.architecture/.cache/git/<name>/`
- Same `YamlRulePackLoader` loads the cached YAML files
- If cache doesn't exist, resolver throws with "Run `arch sync` first"

## New Types

| Type | Project | Purpose |
|------|---------|---------|
| `ImportDefinition` | Core | Model for one entry in `imports.yaml` |
| `ImportsManifest` | Core | Root model: `List<ImportDefinition>` |
| `IGitSyncProvider` | Core | Interface for git operations |
| `GitCliSyncProvider` | Core | Implementation using `git` CLI |
| `SemVerRange` | Core | Parse and match semver constraints |
| `SyncResult` | Core | Result of a single sync operation |
| `SyncCommand` | Cli | `arch sync` command |
| `ImportsYamlLoader` | Core | Deserialize `imports.yaml` |
