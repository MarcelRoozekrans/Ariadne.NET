# Git Sync

The sync system allows teams to publish and consume rule packs via git repositories with semver-aware version resolution and local caching.

## Imports Manifest

**Interface:** `IImportsLoader`
**Implementation:** `ImportsYamlLoader`

Parses `.architecture/imports.yaml` files that define external pack dependencies.

```yaml
# .architecture/imports.yaml
imports:
  - name: company-standards
    type: git
    url: git@github.com:acme/architecture-packs.git
    path: packs/clean-architecture
    version: "^2.0"

  - name: team-conventions
    type: git
    url: https://github.com/acme/team-rules.git
    version: "1.3.0"
```

**ImportDefinition model:**

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | Import identifier (used in `import://name` references) |
| `Type` | `string` | Sync provider type (currently only `"git"` supported) |
| `Url` | `string` | Git repository URL (SSH or HTTPS) |
| `Path` | `string` | Subdirectory within repo containing the pack (default: `""` = root) |
| `Version` | `string` | Semver version or range (`"1.3.0"`, `"^2.0"`, `"~1.3"`) |

## SemVer Range Resolution

**Class:** `SemVerRange`

Parses version range strings and matches against available git tags.

**Supported range syntaxes:**

| Syntax | Meaning | Match Examples |
|--------|---------|----------------|
| `1.3.0` | Exact match | Only `1.3.0` |
| `^2.0` | Compatible (same major) | `2.0.0`, `2.1.0`, `2.9.9` — not `3.0.0` |
| `^0.3` | Compatible (same minor, major=0) | `0.3.0`, `0.3.5` — not `0.4.0` |
| `~1.3` | Approximately (same major.minor) | `1.3.0`, `1.3.9` — not `1.4.0` |
| `~1.3.2` | Approximately (same major.minor) | `1.3.2`, `1.3.9` — not `1.4.0` |

**SemVer parsing:**
- Regex: `^v?(\d+)\.(\d+)(?:\.(\d+))?$`
- Accepts optional `v` prefix (e.g., `v1.0.0`)
- Patch defaults to `0` if omitted (e.g., `1.3` → `1.3.0`)

**FindBestMatch algorithm:**

```mermaid
flowchart LR
    A["Tags: 1.0.0, 1.1.0,<br/>2.0.0, 2.1.0, 3.0.0<br/>Range: ^2.0"] --> B["Filter: IsSatisfiedBy()<br/>>=2.0.0 and <3.0.0"]
    B --> C["Candidates:<br/>2.0.0, 2.1.0"]
    C --> D["Select highest:<br/>2.1.0"]
```

## GitCliSyncProvider

**Interface:** `IGitSyncProvider`
**Implementation:** `GitCliSyncProvider`

Performs the actual git operations to fetch and cache rule packs.

```
IGitSyncProvider
  └── SyncAsync(ImportDefinition import, string cacheDir, bool noCache, CancellationToken ct)
        → Task<SyncResult>
```

**Full sync workflow:**

```mermaid
flowchart TD
    A["SyncAsync(import, cacheDir, noCache)"] --> B{"!noCache AND<br/>cache valid?"}
    B -->|Yes| C["Read .git-info.json<br/>Verify URL + version range<br/>→ Return SyncResult(WasCached=true)"]
    B -->|No| D["Step 2: Fetch remote tags<br/><i>git ls-remote --tags URL</i><br/>Parse + filter valid semver"]
    D --> E["Step 3: Find best match<br/><i>SemVerRange.FindBestMatch(tags)</i>"]
    E --> F{"Match found?"}
    F -->|No| G["Return error SyncResult"]
    F -->|Yes| H["Step 4: Get commit SHA<br/><i>Prefer annotated ^{} tag</i>"]
    H --> I["Step 5: Shallow clone<br/><i>git clone --depth 1<br/>--branch tag --single-branch</i>"]
    I --> J["Step 6: Copy pack files<br/><i>Exclude .git/ directories<br/>Preserve structure</i>"]
    J --> K["Step 7: Write .git-info.json<br/><i>url, resolvedTag, commitSha, syncedAt</i>"]
    K --> L["Step 8: Cleanup<br/><i>Delete temp clone (finally block)</i>"]
    L --> M["Return SyncResult<br/><i>Success=true, WasCached=false</i>"]
```

**Cache validation (IsCacheValid):**

1. Check cache directory exists
2. Read and parse `.git-info.json`
3. Verify URL matches exactly (Ordinal comparison)
4. Parse cached version as SemVer
5. Check version still satisfies the requested range
6. Returns `false` if any check fails (graceful degradation)

**Git command execution (RunGitAsync):**

- Uses `System.Diagnostics.Process` with `UseShellExecute=false`, `CreateNoWindow=true`
- Redirects stdout and stderr
- Reads both streams asynchronously
- Throws `InvalidOperationException` on non-zero exit code with stderr details
- Uses the caller's git credentials (SSH keys, credential managers)

## SyncOrchestrator

**Interface:** `ISyncOrchestrator`
**Implementation:** `SyncOrchestrator`

Orchestrates syncing of all or specific imports from the manifest.

```
ISyncOrchestrator
  ├── SyncAllAsync(manifest, cacheDir, noCache, ct) → Task<IReadOnlyList<SyncResult>>
  └── SyncByNameAsync(manifest, name, cacheDir, noCache, ct) → Task<IReadOnlyList<SyncResult>>
```

**Type routing:** Only `"git"` type is supported. Other types return an error `SyncResult`.

**SyncByNameAsync:** Finds the import by name (case-insensitive), returns error if not found.

---

Previous: [Validation and Linting](08-validation.md) | Next: [Host Applications](10-host-applications.md)
