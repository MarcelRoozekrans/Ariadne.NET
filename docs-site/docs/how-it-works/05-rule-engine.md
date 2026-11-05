---
sidebar_position: 5
---

# Rule Engine

The rule engine provides two core services: pack merging and layer inference. Checking existing code against the layer rules is done by the semantic validator; see [Validation and Linting](08-validation.md).

## RulePackMerger

**Interface:** `IRulePackMerger`
**Implementation:** `RulePackMerger`

Combines multiple rule packs into a single resolved pack using deterministic merge semantics.

```
IRulePackMerger
  ├── Merge(IReadOnlyList<RulePack> packs) → RulePack
  └── MergeWithDiagnostics(IReadOnlyList<RulePack> packs) → MergeResult
```

**Merge algorithm:**

```mermaid
flowchart TD
    A["Input: [pack-A, pack-B, pack-C]<br/><i>ordered: first is base, last is overlay</i>"] --> B["1. Name<br/>Join with '+' → pack-A+pack-B+pack-C"]
    B --> C["2. Merge Layers<br/><i>By name (case-insensitive)</i>"]
    C --> D["3. Merge Component Roles<br/><i>By name (case-insensitive)</i>"]
    D --> E["4. Merge Code Feature Rules<br/><i>By ID (case-insensitive)</i>"]
    E --> F["5. Naming Conventions<br/><i>Last pack wins</i>"]
    F --> G["MergeResult<br/><i>MergedPack + Warnings</i>"]
```

**Merge strategies per element:**

```mermaid
graph TD
    subgraph Layers["Layers — Union"]
        LA["pack-A: Domain deps=[]"] --> LR["Result: Domain deps=[Shared]"]
        LB["pack-B: Domain deps=[Shared]"] --> LR
    end

    subgraph Roles["Component Roles — Last Wins"]
        RA["pack-A: Entity typeKind=Class"] --> RR["Result: Entity typeKind=Record<br/><i>(pack-B wins)</i>"]
        RB["pack-B: Entity typeKind=Record"] --> RR
    end

    subgraph Rules["Code Feature Rules — Locked Protection"]
        FA["pack-A: CODE-001 locked=true"] --> FR["Result: CODE-001 from pack-A<br/><i>(locked, warning issued)</i>"]
        FB["pack-B: CODE-001 override attempt"] -.->|blocked| FR
    end
```

**MergeResult model:**

```
MergeResult
  ├── MergedPack: RulePack         (the combined result)
  └── Warnings: IReadOnlyList<MergeWarning>  (locked rule override attempts)
```

## LayerInferenceEngine

**Interface:** `ILayerInferenceEngine`
**Implementation:** `LayerInferenceEngine`

Resolves which architectural layer a component belongs to based on its role name. Uses a three-phase search with ambiguity detection.

```
ILayerInferenceEngine
  └── Infer(RulePack resolvedPack, string roleName) → LayerInferenceResult
```

**Three-phase search algorithm:**

```mermaid
flowchart TD
    A["Input: roleName='Repository'"] --> B["PHASE 1: Search by primary Name<br/><i>(case-insensitive)</i>"]
    B --> B1{"Matches found?"}
    B1 -->|Exactly 1| OK1["Ok<br/>layer=Infrastructure<br/>roleName=Repository"]
    B1 -->|Multiple| AMB1["Ambiguous<br/>options=[...]"]
    B1 -->|None| C["PHASE 2: Search by AlternativeNames<br/><i>(case-insensitive)</i>"]
    C --> C1{"Matches found?"}
    C1 -->|Exactly 1| OK2["Ok<br/>layer=Domain<br/>roleName=RepositoryInterface"]
    C1 -->|Multiple| AMB2["Ambiguous<br/>options=[...]"]
    C1 -->|None| D["PHASE 3: Not found<br/>Invalid<br/>Unknown role: Repository"]
```

**LayerInferenceResult model:**

| Field | Type | Description |
|-------|------|-------------|
| `Status` | `LayerInferenceStatus` | `Ok`, `Ambiguous`, or `Invalid` |
| `Layer` | `string?` | Resolved layer name (only when `Ok`) |
| `RoleName` | `string?` | Canonical role name (only when `Ok`) |
| `AmbiguousOptions` | `IReadOnlyList<string>?` | Formatted list of matches (only when `Ambiguous`) |
| `Message` | `string?` | Error or ambiguity description |

**Why alternative names matter:**

In Clean Architecture, the `RepositoryInterface` role has `alternativeNames: [Repository]`. This means if a user asks to generate a "Repository", the engine first finds the `Repository` implementation role (Phase 1 match), but if that role doesn't exist, it falls back to `RepositoryInterface` via alternative name matching (Phase 2).

---

