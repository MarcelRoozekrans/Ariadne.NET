# Ariadne.NET.Abstractions

The rule-pack schema and contracts for [Ariadne.NET](https://github.com/MarcelRoozekrans/architecture-governance-engine), an architecture governance engine driven by declarative YAML rule packs.

Reference this package when your tool needs to talk about rule packs without pulling in the engine. It has no package dependencies and is Native AOT compatible.

## Contents

All types live in the `Ariadne.NET.Abstractions` namespace.

- **Schema:** `RulePack`, `LayerDefinition`, `ComponentRoleDefinition`, `CodeFeatureRuleDefinition`, `NamingConventions`, `CSharpTypeKind`
- **Contracts:** `IRulePackLoader`, `IRulePackResolver`, `IBuiltinPackRegistry`

Loading YAML, resolving `extends`, merging packs, the built-in packs and validation live in the [`Ariadne.NET`](https://www.nuget.org/packages/Ariadne.NET) engine package, which implements these contracts.
