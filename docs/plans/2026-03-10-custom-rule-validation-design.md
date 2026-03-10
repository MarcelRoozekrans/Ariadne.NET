# Custom Rule Validation — Design Document

**Date:** 2026-03-10
**Status:** Approved

## Goal

Enforce ComponentRoleDefinition constraints (interface requirements, accessibility, type kind) against actual source code by extending the existing SourceValidator with Roslyn-based type declaration analysis.

## The Gap

ComponentRoleDefinitions declare rich structural constraints:
- `requiresInterface: true` with `interfaceRole: RepositoryInterface`
- `accessibility: internal`
- `typeKind: Record` / `Class` / `Interface` / `Struct`

Currently, none of these are validated against source code. The SourceValidator only checks layer dependency rules via `using` directives. CodeFeatureRules are loaded, merged, and exposed via API/MCP but never enforced.

## Design

### Role Matching

Suffix-based matching: type names are matched to component roles by suffix, scoped to the file's detected layer.

| Source Type | Layer | Matched Role |
|------------|-------|-------------|
| `OrderRepository` | Infrastructure | Repository |
| `IOrderRepository` | Domain | RepositoryInterface |
| `CreateOrderHandler` | Application | CommandHandler |
| `OrderCreated` | Domain | DomainEvent |

Matching algorithm:
1. Detect the file's layer from its namespace (existing logic)
2. For each type declaration in the file, try to match its name suffix against role names in that layer
3. Also check `AlternativeNames` on each role
4. Case-insensitive suffix matching
5. If no role matches, skip the type (not all types need to match a role)

### Three New Checks

#### 1. Interface Requirement (`MissingInterface`)

If a matched role has `requiresInterface: true` and `interfaceRole: "RepositoryInterface"`:
- Look for an interface type in the same file or across scanned files whose name matches the interface role suffix
- Report violation if no matching interface found in the codebase

#### 2. Accessibility (`AccessibilityMismatch`)

If a matched role specifies `accessibility: internal`:
- Check the Roslyn syntax node's modifiers for `internal` keyword
- Report violation if the type is `public` when the role says `internal` (or vice versa)
- Default role accessibility is `public`

#### 3. Type Kind (`TypeKindMismatch`)

If a matched role specifies `typeKind: Record`:
- Check the Roslyn syntax node type (ClassDeclarationSyntax, RecordDeclarationSyntax, InterfaceDeclarationSyntax, StructDeclarationSyntax)
- Report violation if the actual syntax kind doesn't match the declared type kind

### Integration into SourceValidator

Extend `ValidateFile` to:
1. After existing dependency checks, iterate type declarations in the syntax tree
2. Match each type to a component role by suffix + layer
3. For matched types, run all three checks
4. Add violations to the existing `List<SourceViolation>`

Reuses existing `SourceValidationResult` and `SourceViolation` — just new `ViolationType` string values.

### New Violation Types

| ViolationType | Message Example |
|--------------|----------------|
| `MissingInterface` | "Type 'OrderRepository' matches role 'Repository' which requires interface 'RepositoryInterface', but no matching interface was found" |
| `AccessibilityMismatch` | "Type 'OrderRepository' matches role 'Repository' and should be 'internal' but is declared 'public'" |
| `TypeKindMismatch` | "Type 'CreateOrderCommand' matches role 'Command' and should be a 'Record' but is declared as a 'Class'" |

## What Changes

- `SourceValidator.cs` — add role matching + three validation checks
- Extract helpers: `MatchTypeToRole`, `CheckInterfaceRequirement`, `CheckAccessibility`, `CheckTypeKind`
- New unit tests for each violation type
- No API/CLI changes needed — validate command already returns all violations

## Out of Scope

- Enforcing CodeFeatureRules' `AppliesTo` dictionary generically (the current rules like CODE-001/CODE-002 describe the same checks we're implementing directly)
- Cross-file interface resolution (check within same validation run's file set only)
- Auto-fix suggestions
