---
sidebar_position: 8
---

# Validation and Linting

The engine provides two levels of source code analysis: full validation (deep) and linting (shallow). Both use Roslyn to parse C# source files — no regex-based analysis.

## SourceValidator — Deep Validation

**Interface:** `ISourceValidator`
**Implementation:** `SourceValidator`

Performs comprehensive validation of existing source code against architecture rules.

```
ISourceValidator
  └── Validate(RulePack resolvedPack, string sourceDirectory) → SourceValidationResult
```

**Two-pass algorithm:**

```mermaid
flowchart TD
    subgraph Pass1["PASS 1: Per-file analysis and role matching"]
        A["For each *.cs file (recursive)"] --> B["Parse with CSharpSyntaxTree"]
        B --> C["Extract namespace<br/><i>(file-scoped or block)</i>"]
        C --> D["Detect layer from namespace segments"]
        D --> E["Validate using directives"]
        E --> E1["For each 'using' statement:<br/>- Detect target layer<br/>- Skip self-references<br/>- Check allowedDependencies<br/>- If not → DependencyViolation"]
        D --> F["Validate type declarations"]
        F --> F1["For each type:<br/>- Match name to role (by suffix)<br/>- Check TypeKind matches<br/>- Check accessibility matches<br/>- Record matched roles"]
    end

    subgraph Pass2["PASS 2: Cross-file interface requirement check"]
        G["For each role with<br/>RequiresInterface=true"] --> H{"Implementations<br/>found in Pass 1?"}
        H -->|Yes| I{"Corresponding<br/>interface found?"}
        H -->|No| J["Skip — no check needed"]
        I -->|Yes| K["Valid"]
        I -->|No| L["MissingInterface violation"]
    end

    Pass1 --> Pass2
```

**Layer detection algorithm (DetectLayer):**

```mermaid
flowchart LR
    A["Namespace:<br/>MyApp.Orders.Infrastructure"] --> B["Split by '.'<br/>[MyApp, Orders, Infrastructure]"]
    B --> C["Scan right-to-left"]
    C --> D{"Infrastructure<br/>in knownLayers?"}
    D -->|Yes| E["Return: Infrastructure"]
```

**Type-to-role matching algorithm (MatchTypeToRole):**

```mermaid
flowchart TD
    A["typeName: OrderRepository<br/>detectedLayer: Infrastructure"] --> B["For each role in ComponentRoles"]
    B --> C{"role.Layer ==<br/>Infrastructure?"}
    C -->|No| B
    C -->|Yes| D{"typeName ends with<br/>role.Name?"}
    D -->|Yes| E["Match found:<br/>Role 'Repository'<br/>Layer 'Infrastructure'"]
    D -->|No| F{"typeName ends with<br/>alternativeName?"}
    F -->|Yes| E
    F -->|No| B
```

**Violation types:**

| Type | Meaning | Example |
|------|---------|---------|
| `DependencyViolation` | `using` directive violates layer dependencies | Domain file using Infrastructure namespace |
| `TypeKindMismatch` | Type declaration doesn't match role's expected kind | Role says `Interface` but file has `class` |
| `AccessibilityMismatch` | Access modifier doesn't match role definition | Role says `internal` but type is `public` |
| `MissingInterface` | Role requires interface but none found in codebase | `Repository` exists but `IRepository` doesn't |

**Result model:**

```
SourceValidationResult
  ├── IsValid: bool                     (true if no violations)
  ├── Violations: IReadOnlyList<SourceViolation>
  │     ├── FilePath: string
  │     ├── ViolationType: string
  │     └── Message: string
  └── FilesScanned: int
```

## Linter — Shallow Validation

**Interface:** `ILinter`
**Implementation:** `Linter`

Performs a lighter check focused on namespace-vs-directory consistency. Does not check dependencies, type kinds, or interface requirements.

```
ILinter
  └── Lint(RulePack resolvedPack, string sourceDirectory) → LintResult
```

**Algorithm:**

```mermaid
flowchart TD
    A["For each *.cs file (recursive)"] --> B["Parse with Roslyn<br/>Extract namespace"]
    B --> C["Detect layer from<br/>namespace segments"]
    B --> D["Detect layer from<br/>file path segments"]
    C --> E{"Compare layers"}
    D --> E
    E -->|"Both null"| F["UnrecognizedLayer"]
    E -->|"Both non-null<br/>and different"| G["NamespaceMismatch"]
    E -->|"Match or<br/>one is null"| H["Clean"]
```

**Example violation:**

```
File:       src/MyApp.Orders.Domain/OrderRepository.cs
Namespace:  MyApp.Orders.Infrastructure   ← layer = Infrastructure
Directory:  src/MyApp.Orders.Domain/       ← layer = Domain

→ NamespaceMismatch: "Namespace implies layer 'Infrastructure' but directory implies 'Domain'"
```

**Result model:**

```
LintResult
  ├── IsClean: bool
  ├── Violations: IReadOnlyList<LintViolation>
  │     ├── FilePath: string
  │     ├── Category: string
  │     └── Message: string
  └── FilesScanned: int
```

---

