# Ariadne.NET.Abstractions

The rule-pack schema and contracts for [Ariadne.NET](https://github.com/MarcelRoozekrans/Ariadne.NET), an architecture governance engine driven by declarative YAML rule packs.

Reference this package when your tool needs to talk about rule packs without pulling in the engine. Its only package dependencies are `ZeroAlloc.Results` and `ZeroAlloc.Validation` (which bundles its generator), and it is Native AOT compatible.

## Contents

All types live in the `Ariadne.NET.Abstractions` namespace.

- **Schema:** `RulePack`, `LayerDefinition`, `ComponentRoleDefinition`, `RuleDefinition`, `TargetSelector`, `RuleException`, `NamingConventions`, `CSharpTypeKind`
- **Contracts:** `IRulePackLoader`, `IRulePackResolver`, `IBuiltinPackRegistry`, with `AriadneError` as the error type of their `Result<T, AriadneError>` returns

Loading YAML, resolving `extends`, merging packs, the built-in packs and validation live in the [`Ariadne.NET`](https://www.nuget.org/packages/Ariadne.NET) engine package, which implements these contracts.
