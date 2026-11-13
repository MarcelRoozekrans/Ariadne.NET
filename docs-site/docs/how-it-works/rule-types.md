---
sidebar_position: 3.5
---

# Rule Types

This page is the reference for the `rules:` list of a [schema 2 pack](03-rule-schema.md). Each rule type below has its fields, an example, the violation it reports and its limits. A [JSON Schema](editor-support.md) checks the shape of a pack in your editor.

## Rules

A rule is an entry in `rules:`. It has exactly one of `forbid`, `allowOnly`, `require`, `bannedPackages`, `uniqueNames` or `slices`.

| Key | Required | Meaning |
|-----|----------|---------|
| `id` | yes | Unique across all layer and rule ids of the merged pack. |
| `because` | yes | Why the rule exists. Repeated on every violation. |
| `description` | no | Free text. |
| `forbid` | one of | `{ from: <target>, to: <target> }`: no type in `from` may depend on a type in `to`. Reported as `ForbiddenDependency`. |
| `allowOnly` | one of | `{ from: <target>, to: <target> }`: a type in `to` may be depended on only by types in `from`, or by types in `to` itself. Any other source is reported as `DependencyNotPermitted`. |
| `require` | one of | `{ for: <selector>, layer, kind, accessibility, implements, is, isNot }`: every type that `for` selects must satisfy the requirement. At least one of `layer`, `kind`, `accessibility`, `implements`, `is` and `isNot` is needed. See [Require rules](#require-rules). |
| `bannedPackages` | one of | `{ packages: [...], from: { assemblies: [...] } }`: no project in `from` may resolve a package that `packages` matches. Reported as `BannedPackage`. See [Banned packages](#banned-packages). |
| `uniqueNames` | one of | `{ for: <target>, against: <target> }`: no type in `for` may share a simple name with a type in `against`. Reported as `NameCollision`. See [Unique names](#unique-names). |
| `slices` | one of | `{ matching: [...], freeOfCycles or independent, allow }`: cuts the code into slices and keeps them free of cycles or independent. Reported as `SliceCycle` or `SliceDependency`. See [Slices](#slices). |
| `exceptions` | no | List of selectors, each with a required `because`. A source type that an exception matches is exempt from the rule. On a `bannedPackages` rule an exception takes `assemblies` only, and on a `uniqueNames` rule `namespaces`, `types` and the type traits. |
| `locked` | no, default `false` | A child pack cannot add exceptions to this rule. |
| `allowEmpty` | no, default `false` | Accepts that the rule selects no type. See [Rules that select nothing](#rules-that-select-nothing). |
| `requireTarget` | no, default `false` | On a `forbid`, `allowOnly` or `uniqueNames` rule: fails the run when the rule's target matches no type. See [A target that matches nothing](#a-target-that-matches-nothing). Any other rule kind rejects `true`. |

A **target** is either `layers: [...]` or a [selector](03-rule-schema.md#selectors) made of `namespaces`, `assemblies`, `types`, `baseTypes`, `interfaces`, `attributes` and the [type traits](03-rule-schema.md#type-traits). `layers` may sit next to traits, which keeps the types of those layers that match every trait. Mixing `layers` with the other selector kinds in one target is a load error, and a target must select something.

An exception takes `namespaces`, `assemblies`, `types` and the [type traits](03-rule-schema.md#type-traits) (at least one key) and a required `because`; the keys of one exception are AND-ed. On a `bannedPackages` rule an exception takes only `assemblies`, and on a `uniqueNames` rule `namespaces`, `types` and the traits. Exceptions exempt the source type, the same way for `forbid` and `allowOnly`. A negative predicate on a list trait is an exception that names the trait: "no type may depend on `Shop.Legacy.**`, except types assignable to `ILegacyAdapter`" is a `forbid` with this exception, which exempts every depending type assignable to `ILegacyAdapter`.

```yaml
rules:
  - id: SHOP-NO-LEGACY
    because: Legacy code is being retired.
    forbid:
      from: { namespaces: ["Shop.**"] }
      to: { namespaces: ["Shop.Legacy.**"] }
    exceptions:
      - assignableTo: ["Shop.Legacy.ILegacyAdapter"]
        because: Adapters are the one way across.
```

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

The C# model is `RuleDefinition`, with `Id`, `Because`, `Description`, `Kind` (`RuleKind.Forbid`, `RuleKind.AllowOnly`, `RuleKind.Require`, `RuleKind.BannedPackages`, `RuleKind.UniqueNames` or `RuleKind.Slices`), `From` and `To` (`TargetSelector`), `Require` (`RuleRequirement?`, with `Layer`, `Kind`, `Accessibility`, `Implements`, `Is` and `IsNot`), `Packages` (`IList<string>`), `Against` (`TargetSelector`), `Slices` (`SliceDefinition?`, with `Matching`, `FreeOfCycles`, `Independent` and `Allow`), `Exceptions` (`IList<RuleException>`), `Locked` and `AllowEmpty`. For a `require` or `uniqueNames` rule `From` holds the `for` selector, and for a `bannedPackages` rule it holds `from`.

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
| `is` | A selector that every selected type must match. See [is and isNot](#is-and-isnot). | `RequirementNotMet` |
| `isNot` | A selector that no selected type may match. See [is and isNot](#is-and-isnot). | `RequirementNotMet` |

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

#### is and isNot

`is` and `isNot` take a selector with every key except `layers` and `members`: the [type traits](03-rule-schema.md#type-traits) and also `namespaces`, `assemblies`, `types`, `baseTypes`, `interfaces` and `attributes`.

```yaml
rules:
  - id: SHOP-DOMAIN-SEALED
    because: Domain classes are not extension points.
    require:
      for: { namespaces: ["Shop.Domain.**"], kinds: [class, record] }
      is: { sealed: true }
      isNot: { names: ["*Impl", "*Helper"] }
```

- **Each key is its own requirement.** In `is`, a type must match every key, and each key it fails is one violation. In `isNot`, a type must match none of the keys, and each key it matches is one violation. Within one key the values stay alternatives, so `isNot: { names: [A, B] }` means "matches neither A nor B".
- **The message names the key.** It reads `Type 'OrderService' is selected by rule 'SHOP-DOMAIN-SEALED' and should be sealed but is not` for `is`, and `Type 'OrderImpl' is selected by rule 'SHOP-DOMAIN-SEALED' and should not have a name matching '*Impl' or '*Helper' but is named 'OrderImpl'` for `isNot`. A boolean that is `false` reads in its positive form, so `is: { sealed: false }` reports `should not be sealed but is`.
- **They apply to every selected type, enums and delegates included.** The older keys `kind`, `accessibility` and `implements` stay as shorthand and keep skipping enums and delegates, so `kind: class` over an enum reports nothing while `is: { kinds: [class] }` over the same enum reports a violation.
- **`NotExist`.** To say that no type may exist in a place, select the types and refuse all of them with `allowEmpty: true`, because an empty selection is then the goal and not a mistake:

```yaml
rules:
  - id: SHOP-NO-LEGACY-NAMESPACE
    because: The legacy namespace was removed.
    allowEmpty: true
    require:
      for: { namespaces: ["Shop.Legacy.**"] }
      isNot: { types: ["**"] }
```

A `require` rule yields one violation per selected type and failed requirement, with the rule's `id` and `because`. Exceptions exempt a type. A rule whose `for` selects no type is reported as vacuous; see [Rules that select nothing](#rules-that-select-nothing).

### Rules that select nothing

A rule, a component role or a layer that selects no type cannot fail, so a clean report would say nothing about it. The run therefore fails: each such element is reported as a violation without a file or line, and `validate` exits non-zero.

| Kind | Reported for | Message |
|------|--------------|---------|
| `VacuousRule` | a `forbid` or `allowOnly` rule whose `from` selects no type | `Rule '<id>' selects no type: its 'from' selector matches nothing in the <n> project(s) loaded.` |
| `VacuousRule` | a `require` or `uniqueNames` rule whose `for` selects no type | `Rule '<id>' selects no type: its 'for' selector matches nothing in the <n> project(s) loaded.` |
| `VacuousRule` | a `bannedPackages` rule whose `from.assemblies` matches no loaded project | `Rule '<id>' selects no type: its 'from.assemblies' matches none of the <n> project(s) loaded.` |
| `VacuousRule` | a `slices` rule whose `matching` patterns place no type in a slice | `Rule '<id>' selects no type: its 'matching' patterns place no type in a slice in the <n> project(s) loaded.` |
| `VacuousRule` | a component role that matches no type in its layer | `Role '<Name>' matches no type in layer '<Layer>' in the <n> project(s) loaded.` |
| `VacuousLayer` | a layer that covers no type | `Layer '<Name>' covers no type in the <n> project(s) loaded.` |

Only the subject side is checked: the `from` of `forbid`, `allowOnly`, `require` and `uniqueNames` (for the last two, the `for` selector), and the `matching` patterns of `slices`. The `to`, `against` and `members` sides are not checked unless the rule sets `requireTarget: true` (`forbid`, `allowOnly` and `uniqueNames`; it checks only that target selector, see [a target that matches nothing](#a-target-that-matches-nothing)), and the `exceptions` are never checked, since a rule about something that does not exist yet is still sound. A `bannedPackages` rule is vacuous only when it has a non-empty `from.assemblies` that matches no loaded project; without `from` it applies to every project. A rule or role whose subject resolves only through layers that are already reported as vacuous gets no second violation, because fixing the layer fixes it.

Vacuity is relative to the projects loaded. A run on a single `.csproj` reports as vacuous every layer whose types the loaded projects neither declare nor reference, and the message says how many projects were loaded. A layer in another project still counts as covered when a loaded project references its types. A layer that holds only generated types is vacuous, since generated code is not analyzed. Validate the solution, or mark the rules and roles that belong to other projects.

To accept a rule or role that selects nothing, set `allowEmpty: true` on it. A layer has no opt-out: retarget it in the pack that extends the base pack, so that it covers the types of the solution. The built-in packs set `allowEmpty: true` on every role, because a solution rarely has all of them. The `rules` command and the `get_rules` tool show `allowEmpty` and `requireTarget` only when they are `true`.

```yaml
rules:
  - id: SHOP-NO-DIRECT-SQL
    because: Data access goes through repositories.
    allowEmpty: true
    forbid:
      from: { namespaces: ["Shop.Web.**"] }
      to: { namespaces: ["System.Data.SqlClient"] }
```

### A target that matches nothing

A `forbid` of a library that nobody uses is sound, so the target side is not checked by default. When a rule only means something if its target is in use, set `requireTarget: true` on it. A `forbid`, `allowOnly` or `uniqueNames` rule accepts the key; a `require`, `bannedPackages` or `slices` rule rejects `true` with the load error `'requireTarget' applies only to forbid, allowOnly and uniqueNames rules`.

```yaml
rules:
  - id: AGENTS-ONE-SEAM
    because: Subagents run through one seam.
    requireTarget: true
    allowOnly:
      from: { namespaces: ["Agents.Runtime.**"] }
      to: { types: ["Thalos.ISubagentRunner"] }
```

The run then looks for the target in every project loaded:

- `to` of a `forbid` or `allowOnly` rule is found when a type it selects is declared or referenced anywhere in the solution. When `to` names `members`, it is found only when some code uses one of those members.
- `against` of a `uniqueNames` rule is found only by a declared type, because that rule compares declared types.

This proves that the solution uses the target somewhere, not which project does. A project that does not touch the target still passes when another project references it. A target is absent only when the solution neither declares nor references it. A guard on the solution's own types therefore works before anything uses them, since a declared type is found. A library that the solution reaches only transitively is never found, because the analysis sees the types that code writes. In Daedalus, Rag.NET is reached only through an adapter package, so the rule that forbids it does not set the key.

Set `requireTarget` for a seam that the solution uses, such as a single-seam `allowOnly` to `Thalos.ISubagentRunner`, where a rename of the type would silently end the protection. Leave it off for a preventive `forbid` of a library that nobody uses yet.

A missing target is one `VacuousRule` violation without a file or line, with the rule's `id` and `because`:

| Rule | Message |
|------|---------|
| `forbid` or `allowOnly` | `Rule '<id>' requires its target, but its 'to' selector matches no type declared or referenced in the <n> project(s) loaded.` |
| `forbid` or `allowOnly` with `members` in `to` | `Rule '<id>' requires its target, but no code in the <n> project(s) loaded uses a member its 'to' selector names.` |
| `uniqueNames` | `Rule '<id>' requires its target, but its 'against' selector matches no type declared in the <n> project(s) loaded.` |

`allowEmpty` does not excuse the target, since it is about the subject: a rule with both keys and an empty `from` and `to` reports one violation, for the target. When `to` names only layers that are already reported as `VacuousLayer`, the target is not reported again.

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

### Slices

A `slices` rule cuts the types of the solution into slices by a capture in a namespace pattern, and then checks how the slices depend on each other. It is the rule for a modular monolith: every module is a slice, and the rule keeps the modules apart.

```yaml
rules:
  - id: SHOP-MODULES-INDEPENDENT
    because: A module is reached only through its Public namespace.
    slices:
      matching: ["Shop.(*).**"]
      independent: true
      allow: { namespaces: ["Shop.*.Public.**"] }
```

| Key | Required | Meaning |
|-----|----------|---------|
| `matching` | yes | A non-empty list of namespace patterns. Each has exactly one capture. |
| `freeOfCycles` | one of | `true`: slices may depend on each other, but not in a cycle. |
| `independent` | one of | `true`: slices may not depend on each other at all, except on what `allow` selects. A dependency cycle then shows up as the dependencies it consists of, not as a `SliceCycle`. |
| `allow` | no | A selector with the keys of the `to` of a `forbid` rule, `layers` included and `members` excluded, and at least one key. A dependency on a type it selects is not reported. |

Exactly one of `freeOfCycles` and `independent` is set, and it must be `true`: leave a condition out instead of writing `false`.

**Captures.** The pattern is a dotted glob in which one segment is `(*)` or `(**)`. `(*)` captures exactly one segment and `(**)` one or more. The capture must be a whole segment, so `Shop.Ord(*)` is a load error, and a pattern needs exactly one. A capture never matches zero segments. The captured text is the name of the slice. `(*)` and `(**)` are accepted only in `matching`; in a slices rule the loader rejects them in `allow` and in `exceptions`. In every other rule they stay ordinary glob text.

Two tie-breaks decide the capture when a pattern could match a namespace in more than one way. The earliest capture start wins. For `(**)`, the shortest capture that lets the rest of the pattern match wins. When several patterns match a namespace, the first one in list order names the slice. Two patterns that capture the same text name one slice.

```yaml
rules:
  - id: SHOP-MODULES-ACYCLIC
    because: Modules form a hierarchy.
    slices:
      matching: ["Shop.(**).Core.**", "Shop.(**).Public.**"]
      freeOfCycles: true
```

With `Shop.(**).Core.**`, the namespace `Shop.Orders.Core.Models` is in slice `Orders`, and `Shop.Sales.Eu.Core` is in slice `Sales.Eu`. Without the trailing `.**`, only types directly in `Shop.<x>.Core` would be in a slice, and `Shop.Orders.Core.Models` would be in none.

**Which types have a slice.** Only the types that the projects of the run declare. A framework type or a NuGet type has no slice, whatever its namespace looks like, so `System.Foo.Core.Bar` is never a target in another slice. A nested type has the namespace of its outermost containing type. A type whose namespace matches no pattern has no slice, and a dependency between two types is checked only when both have one. Only type references count, not member uses.

**`independent` and `freeOfCycles`.** Under `independent`, each dependency from a type in one slice to a type in another is a `SliceDependency` violation, one per source and target type, unless `allow` selects the target. Under `freeOfCycles`, slice-to-slice dependencies are fine until they form a cycle. A `SliceCycle` violation is one per slice edge inside a cycle, and its message names every slice of the cycle. A slice that only points into a cycle is not part of it, and its edge is not reported. The types that `allow` selects are left out of the cycle check as well.

**Exceptions** exempt the source type, as in `forbid`: an excepted type keeps its slice and still counts as a target of other slices.

**Across projects.** Slices are not bound to a project. The edges of all projects of the run are combined, so a cycle `Orders` to `Billing` to `Orders` is found even when three projects each hold one edge. A project built for several target frameworks is checked once per framework, together with the projects that name none, and a violation found in several frameworks is reported once with its `TargetFrameworks`.

| Kind | Reported for | Message |
|------|--------------|---------|
| `SliceDependency` | a type in a slice that depends on a type in another slice, under `independent` | `Type '<source>' in slice '<a>' depends on '<target>' in slice '<b>', and rule '<id>' keeps slices independent` |
| `SliceCycle` | a slice edge that lies inside a cycle, under `freeOfCycles` | `Slice '<a>' depends on slice '<b>' (first through '<source>' -> '<target>'), and slices <list> form a cycle, which rule '<id>' forbids` |

A slices rule that places no type in a slice is reported as `VacuousRule`; see [Rules that select nothing](#rules-that-select-nothing).

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


### Keeping sensitive APIs in Infrastructure

Three `allowOnly` rules confine the APIs a team usually wants behind one layer: SQL, cryptography and HTTP. Each says that only the Infrastructure layer may use the target, and every other layer that does fails with `DependencyNotPermitted` and the rule's `because`.

```yaml
rules:
  - id: SHOP-SQL-ONLY-IN-INFRASTRUCTURE
    because: Raw SQL stays behind the repositories.
    allowOnly:
      from: { layers: [Infrastructure] }
      to: { members: ["System.Data.Common.DbCommand.Execute*", "Microsoft.Data.SqlClient.SqlCommand.new"] }
  - id: SHOP-CRYPTO-ONLY-IN-INFRASTRUCTURE
    because: Key handling and hashing live in one audited place.
    allowOnly:
      from: { layers: [Infrastructure] }
      to: { namespaces: ["System.Security.Cryptography.**"] }
  - id: SHOP-HTTP-ONLY-IN-INFRASTRUCTURE
    because: Outbound calls go through gateways, so timeouts and retries are set once.
    allowOnly:
      from: { layers: [Infrastructure] }
      to: { namespaces: ["System.Net.Http.**"] }
```

The SQL rule names the members that run a command with `members`; the other two name a whole namespace. They end in `.**` because a namespace pattern without it matches that namespace only, so `System.Net.Http` alone would let `System.Net.Http.Headers` escape the rule. A rule that selects nothing fails with `VacuousRule`, so a solution that never touches cryptography sets `allowEmpty: true` on that rule.
