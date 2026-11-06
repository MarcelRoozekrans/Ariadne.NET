# Ariadne.NET.Abstractions

The rule-pack schema and contracts for [Ariadne.NET](https://github.com/MarcelRoozekrans/Ariadne.NET), an architecture governance engine driven by declarative YAML rule packs.

Reference this package when your tool needs to talk about rule packs without pulling in the engine. Its only package dependencies are `ZeroAlloc.Results` and `ZeroAlloc.Validation` (which bundles its generator), and it is Native AOT compatible.

## Contents

All types live in the `Ariadne.NET.Abstractions` namespace.

- **Schema:** `RulePack`, `LayerDefinition`, `ComponentRoleDefinition`, `RuleDefinition`, `RuleKind`, `RuleRequirement`, `TargetSelector`, `RuleException`, `NamingConventions`, `CSharpTypeKind`, and `LayerRetarget` and `RuleAmendment`, which hold the entries of a child pack that change what it inherits
- **Contracts:** `IRulePackLoader`, `IRulePackResolver`, `IBuiltinPackRegistry`, with `AriadneError` as the error type of their `Result<T, AriadneError>` returns

The model is schema 2: layers with selectors, an `id` and a `because`, and `forbid`, `allowOnly` and `require` rules. See the [rule schema](https://marcelroozekrans.github.io/Ariadne.NET/how-it-works/rule-schema) page, and [Migrating from v1](https://marcelroozekrans.github.io/Ariadne.NET/how-it-works/rule-schema#migrating-from-v1) for packs from release 0.3.0 or earlier.

Loading YAML, resolving `extends`, merging packs, the built-in packs and validation live in the [`Ariadne.NET`](https://www.nuget.org/packages/Ariadne.NET) engine package, which implements these contracts.
