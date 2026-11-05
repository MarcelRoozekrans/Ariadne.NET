---
sidebar_position: 1
---

# Overview

> **Core Principle:** AI generates behavior. The engine governs structure.

AI coding agents generate behavior effectively but are unreliable at structural decisions — they invent folder structures, improvise namespaces, and silently violate dependency rules. This engine separates **structural governance** from AI code generation: the AI declares intent (e.g., "I need a Repository for Orders"), and the engine enforces structure (correct layer, namespace, file path, type kind, accessibility, and interface requirements).

## What the Engine Does

1. **Defines architecture** as declarative YAML rule packs (layers with selectors, forbid and allow-only rules, component roles, naming conventions)
2. **Loads and composes** multiple rule packs with deterministic merge semantics
3. **Generates code** using Roslyn that conforms to the resolved rules
4. **Validates existing solutions** against architecture rules with the compiler's semantic model (layer dependencies, forbid and allow-only rules, type checking, interface requirements)
5. **Syncs rule packs** from git repositories with semver-aware version resolution
6. **Exposes all capabilities** via CLI and an MCP server (stdio and HTTP) for AI agent integration

## What the Engine Does Not Do

- It does not generate method bodies, business logic, or behavior
- It does not enforce runtime rules — all checks are static (compile-time / design-time)
- It does not replace a build system, linter, or test runner — it complements them

---

