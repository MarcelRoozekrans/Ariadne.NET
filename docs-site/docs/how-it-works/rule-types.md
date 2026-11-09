---
sidebar_position: 3.5
---

# Rule Types

This page is the reference for the `rules:` list of a [schema 2 pack](03-rule-schema.md). Each rule type below has its fields, an example, the violation it reports and its limits. A [JSON Schema](editor-support.md) checks the shape of a pack in your editor.

## Rules

A rule is an entry in `rules:`. It has exactly one of `forbid`, `allowOnly`, `require`, `bannedPackages` or `uniqueNames`.

| Key | Required | Meaning |
|-----|----------|---------|
| `id` | yes | Unique across all layer and rule ids of the merged pack. |
| `because` | yes | Why the rule exists. Repeated on every violation. |
| `description` | no | Free text. |
| `forbid` | one of | `{ from: <target>, to: <target> }`: no type in `from` may depend on a type in `to`. Reported as `ForbiddenDependency`. |
| `allowOnly` | one of | `{ from: <target>, to: <target> }`: a type in `to` may be depended on only by types in `from`, or by types in `to` itself. Any other source is reported as `DependencyNotPermitted`. |
| `require` | one of | `{ for: <selector>, layer, kind, accessibility, implements }`: every type that `for` selects must satisfy the requirement. At least one of `layer`, `kind`, `accessibility` and `implements` is needed. See [Require rules](#require-rules). |
| `bannedPackages` | one of | `{ packages: [...], from: { assemblies: [...] } }`: no project in `from` may resolve a package that `packages` matches. Reported as `BannedPackage`. See [Banned packages](#banned-packages). |
| `uniqueNames` | one of | `{ for: <target>, against: <target> }`: no type in `for` may share a simple name with a type in `against`. Reported as `NameCollision`. See [Unique names](#unique-names). |
| `exceptions` | no | List of selectors, each with a required `because`. A source type that an exception matches is exempt from the rule. On a `bannedPackages` rule an exception takes `assemblies` only, and on a `uniqueNames` rule `namespaces` and `types` only. |
| `locked` | no, default `false` | A child pack cannot add exceptions to this rule. |
| `allowEmpty` | no, default `false` | Accepts that the rule selects no type. See [Rules that select nothing](#rules-that-select-nothing). |

A **target** is either `layers: [...]` or a [selector](03-rule-schema.md#selectors) made of `namespaces`, `assemblies`, `types`, `baseTypes`, `interfaces` and `attributes`. Mixing `layers` with selector kinds in one target is a load error, and a target must select something.

An exception takes `namespaces`, `assemblies` and `types` (at least one) and a required `because`; on a `bannedPackages` rule only `assemblies`, and on a `uniqueNames` rule only `namespaces` and `types`. Exceptions exempt the source type, the same way for `forbid` and `allowOnly`.

Rules apply regardless of layers: a `forbid` between two types of the same layer still fires. As with layers, the engine reports one violation per rule, source type and target type, located at the first reference, with the number of references. Each carries the rule's `id` and `because`.

```yaml
rules:
  - id: DAEDALUS-SUBAGENT-RUNNER
    because: Subagents start only through the executor, which records the run.
    allowOnly:
      from: { types: [Daedalus.Application.Agents.SubagentRunExecutor] }
      to: { types: [Daedalus.Application.Agents.ISubagentRunner] }
    exceptions:
      - types: [Daedalus.Infrastructure.Agents.SubagentRunner]
        because: The implementation must name its interface.
      - types: [Daedalus.Api.Composition.AgentRegistration]
        because: DI registration wires the interface to its implementation.
```

The C# model is `RuleDefinition`, with `Id`, `Because`, `Description`, `Kind` (`RuleKind.Forbid`, `RuleKind.AllowOnly`, `RuleKind.Require`, `RuleKind.BannedPackages` or `RuleKind.UniqueNames`), `From` and `To` (`TargetSelector`), `Require` (`RuleRequirement?`, with `Layer`, `Kind`, `Accessibility` and `Implements`), `Packages` (`IList<string>`), `Against` (`TargetSelector`), `Exceptions` (`IList<RuleException>`), `Locked` and `AllowEmpty`. For a `require` or `uniqueNames` rule `From` holds the `for` selector, and for a `bannedPackages` rule it holds `from`.

### Forbid and allowOnly

Both rules constrain dependencies and take `from` and `to`, both required. `from` is a [selector](03-rule-schema.md#selectors) or `layers: [...]`; `to` takes the same keys plus [`members`](#members).

- `forbid` reports `ForbiddenDependency` for each dependency from a type in `from` to a type in `to`.
- `allowOnly` says a type in `to` may be depended on only by types in `from`, or by types in `to` itself. Any other source is reported as `DependencyNotPermitted`.

```yaml
rules:
  - id: SHOP-NO-EFCORE-IN-DOMAIN
    because: Persistence belongs to Infrastructure.
    forbid:
      from: { layers: [Domain] }
      to: { namespaces: ["Microsoft.EntityFrameworkCore.**"] }
  - id: SHOP-REPOSITORIES-ONLY-IN-INFRASTRUCTURE
    because: Callers use the interfaces.
    allowOnly:
      from: { layers: [Infrastructure] }
      to: { types: ["Shop.Infrastructure.Persistence.*Repository"] }
```

The engine reports one violation per rule, source type and target type, located at the first reference, with the number of references. Exceptions exempt the source type. Rules apply regardless of layers, so a `forbid` between two types of the same layer still fires.

### Require rules

A `forbid` or `allowOnly` rule constrains dependencies. A `require` rule constrains where a type lives and what shape it has. `for` is a [selector](03-rule-schema.md#selectors) and the other keys describe what each selected type must be:

| Key | Meaning | Reported as |
|-----|---------|-------------|
| `layer` | The name of the layer the type must belong to. | `WrongLayer` |
| `kind` | `class`, `interface`, `record`, `struct` or `recordStruct`. | `TypeKindMismatch` |
| `accessibility` | `public` or `internal`. Only `internal` is checked, because public is the default. | `AccessibilityMismatch` |
| `implements` | A selector with `namespaces`, `assemblies` and `types` only. The type must implement an interface it matches, including interfaces it gets through a base class. | `MissingInterface` |

Placement example: every implementation of a repository interface lives in Infrastructure, as an internal class.

```yaml
rules:
  - id: SHOP-REPOSITORIES-IN-INFRASTRUCTURE
    because: Persistence is an infrastructure concern, and callers use the interface.
    require:
      for: { interfaces: ["**.I*Repository"] }
      layer: Infrastructure
      kind: class
      accessibility: internal
    exceptions:
      - namespaces: ["**.Domain.**"]
        because: The repository interfaces themselves live in the domain.
      - types: ["Shop.Testing.InMemoryOrderRepository"]
        because: Test doubles live next to the tests.
```

`interfaces` selects a type that implements a matching interface, and that includes an interface that extends one. `interface IOrderRepository : IRepository<Order>` is therefore selected by `**.I*Repository` too, and without the Domain exception it would be reported as `WrongLayer`, `TypeKindMismatch` and `AccessibilityMismatch`.

A `require` rule yields one violation per selected type and failed requirement, with the rule's `id` and `because`. Exceptions exempt a type. A rule whose `for` selects no type is reported as vacuous; see [Rules that select nothing](#rules-that-select-nothing).

### Rules that select nothing

A rule, a component role or a layer that selects no type cannot fail, so a clean report would say nothing about it. The run therefore fails: each such element is reported as a violation without a file or line, and `validate` exits non-zero.

| Kind | Reported for | Message |
|------|--------------|---------|
| `VacuousRule` | a `forbid` or `allowOnly` rule whose `from` selects no type | `Rule '<id>' selects no type: its 'from' selector matches nothing in the <n> project(s) loaded.` |
| `VacuousRule` | a `require` or `uniqueNames` rule whose `for` selects no type | `Rule '<id>' selects no type: its 'for' selector matches nothing in the <n> project(s) loaded.` |
| `VacuousRule` | a `bannedPackages` rule whose `from.assemblies` matches no loaded project | `Rule '<id>' selects no type: its 'from.assemblies' matches none of the <n> project(s) loaded.` |
| `VacuousRule` | a component role that matches no type in its layer | `Role '<Name>' matches no type in layer '<Layer>' in the <n> project(s) loaded.` |
| `VacuousLayer` | a layer that covers no type | `Layer '<Name>' covers no type in the <n> project(s) loaded.` |

Only the subject side is checked: the `from` of `forbid`, `allowOnly`, `require` and `uniqueNames` (for the last two, the `for` selector). The `to`, `against` and `members` sides and the `exceptions` are never checked, since a rule about something that does not exist yet is still sound. A `bannedPackages` rule is vacuous only when it has a non-empty `from.assemblies` that matches no loaded project; without `from` it applies to every project. A rule or role whose subject resolves only through layers that are already reported as vacuous gets no second violation, because fixing the layer fixes it.

Vacuity is relative to the projects loaded. A run on a single `.csproj` reports as vacuous every layer whose types the loaded projects neither declare nor reference, and the message says how many projects were loaded. A layer in another project still counts as covered when a loaded project references its types. A layer that holds only generated types is vacuous, since generated code is not analyzed. Validate the solution, or mark the rules and roles that belong to other projects.

To accept a rule or role that selects nothing, set `allowEmpty: true` on it. A layer has no opt-out: retarget it in the pack that extends the base pack, so that it covers the types of the solution. The built-in packs set `allowEmpty: true` on every role, because a solution rarely has all of them. The `rules` command and the `get_rules` tool show `allowEmpty` only when it is `true`.

```yaml
rules:
  - id: SHOP-NO-DIRECT-SQL
    because: Data access goes through repositories.
    allowEmpty: true
    forbid:
      from: { namespaces: ["Shop.Web.**"] }
      to: { namespaces: ["System.Data.SqlClient"] }
```

### Banned packages

A `bannedPackages` rule bans packages from the resolved package graph of a project. `packages` is a required, non-empty list of package-id patterns, and `from` optionally limits the rule to projects by `assemblies`; any other key in `from` is a load error, because a package belongs to a project and not to a type.

```yaml
rules:
  - id: SHOP-NO-NEWTONSOFT-JSON
    because: JSON goes through System.Text.Json.
    bannedPackages:
      packages: ["Newtonsoft.Json", "FluentValidation*"]
      from: { assemblies: ["Shop.Domain", "Shop.Application"] }
```

Package ids match case-insensitively, since NuGet ids do, with the `*` rules of `types`. Transitive packages count, including a package that arrives through a referenced project, and the violation names the path from the direct reference. A violation is one per project and package, with `SourceType` the project name and `Target` the package id. The packages come from `obj/project.assets.json`, so the project must be restored. When a covering rule meets a project whose packages are unknown, the run fails and does not report a clean result.

### Unique names

A `uniqueNames` rule keeps a type from reusing the name of another type. `for` and `against` are both required and take every selector key. A type in `for` that has the same simple name as a type in `against` is one violation, located at the `for` type, with the other type as `Target`.

```yaml
rules:
  - id: SHOP-WEB-OWN-NAMES
    because: Web maps to its own view models and never copies an application DTO.
    uniqueNames:
      for: { layers: [Web] }
      against: { namespaces: ["Shop.Application.**.Dtos"] }
```

The check is one-way: it reports the `for` type only. It compares the simple name case-sensitively, ignores the namespace and counts generic arity, so `Result` and `Result<T>` differ. A nested type uses its own name, a type in both selections never collides with itself, and the types of all projects of the run are compared.

### Members

`members` is a selector key on the `to` side of `forbid` and `allowOnly`. It names APIs at member level: methods, constructors, properties, fields and events. The loader rejects it on `from`, on layers and on exceptions.

```yaml
rules:
  - id: SHOP-SQL-ONLY-IN-INFRASTRUCTURE
    because: Raw SQL stays behind the repositories.
    allowOnly:
      from: { layers: [Infrastructure] }
      to: { members: ["System.Data.Common.DbCommand.Execute*", "Microsoft.Data.SqlClient.SqlCommand.new"] }
```

A pattern is `Namespace.Type.Member` with the glob rules of `types`: the segments before the last name the type, the last segment names the member. `new` names a constructor, nested types are written with dots, and generic types match without type arguments. A use matches when the used member, or a member it overrides or implements, matches a pattern, so calling `SqlCommand.ExecuteReader()` matches `DbCommand.ExecuteReader`. The walk goes upwards only. Calls, constructor calls, property and field reads and writes, event subscriptions and method groups count as uses; declaring a member does not. A pattern needs at least a type and a member segment, unless it starts with `**`: the loader rejects `Start` alone, since it could never match. A `to` selector with `members` is checked on member uses only, and its other keys test the used member's declaring type. Under `allowOnly` the declaring type and the types nested in it are exempt. Violations are `ForbiddenDependency` and `DependencyNotPermitted`, with the used member as `Target`.

Constructor calls include `new T(...)`, target-typed `new(...)`, the `: base(...)` and `: this(...)` initializers, and the base constructor call C# inserts when a constructor names no base arguments. So `class MyClient : HttpClient { }` uses `System.Net.Http.HttpClient.new`, through its implicit constructor; the use is located at the type's name, or at a declared constructor's name. An indexer is matched by its metadata name: `Item`, or the name `[IndexerName]` gives it, as in `System.String.Chars`.

What is not a use: an attribute constructor (the attribute class is still a type reference); the calls the compiler inserts for `foreach`, `using`, collection initializers, operators and conversions, deconstruction and LINQ query syntax; a `nameof`; and a member that satisfies an interface member only through a derived class (`class C : B, IFoo`, where `B.Foo` implements `IFoo.Foo`, called as `c.Foo()`). Operators are never named by a use, so an operator name such as `op_Addition` in a pattern matches nothing.

