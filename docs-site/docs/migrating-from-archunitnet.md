---
sidebar_position: 7
title: Migrating from ArchUnitNET
---

# Migrating from ArchUnitNET

This page is for a team that checks its architecture with [ArchUnitNET](https://github.com/TNG/ArchUnitNET) tests and wants to move those rules to an Ariadne pack. It covers `TngTech.ArchUnitNET` 0.13.2, the version the [ArchUnitNET parity](archunitnet-parity.md) catalogue pins. The catalogue is the reference for what a pack covers; this page is the how-to.

## What changes

- **Rules move out of the test class.** An ArchUnitNET rule is C# in a test method. An Ariadne rule is YAML in a pack under `.architecture/`, usually `.architecture/rules.yaml`. Every rule has an `id` and a `because`, and the `because` is repeated on every violation.
- **Ariadne reads source, ArchUnitNET reads IL.** Ariadne builds its type graph from the Roslyn compilation of each project. ArchUnitNET reads the compiled assemblies. Most rules give the same answer either way. Where they do not, such as for static classes, records and generic names, the catalogue lists the case under [Divergences](archunitnet-parity.md#divergences).
- **One test runs every rule.** `Ariadne.NET.Testing` checks every pack in `.architecture/` in one call, so there is one test, not one per rule. The failure message lists every violation.
- **Layers are open-world.** A layer lists the layers it may depend on. A dependency on a type that no layer covers is not checked. To forbid a dependency on such a type, cover it with a layer or write a `forbid` rule.

## Switching over, step by step

1. Add the package to the test project:

   ```bash
   dotnet add package Ariadne.NET.Testing
   ```

2. Create the pack. [`ariadne init`](getting-started.md#install-the-tool) writes `.architecture/rules.yaml` from a built-in template, or write it by hand. A minimal pack with two layers and one rule:

   ```yaml
   schema: 2
   name: shop
   layers:
     - name: Domain
       id: SHOP-LAYER-DOMAIN
       because: The domain depends on no other layer.
       assemblies: [Shop.Domain]
     - name: Application
       id: SHOP-LAYER-APPLICATION
       because: Use cases depend only on the domain.
       assemblies: [Shop.Application]
       allowedDependencies: [Domain]
   rules:
     - id: SHOP-NO-EFCORE-IN-DOMAIN
       because: Persistence belongs to Infrastructure.
       forbid:
         from: { layers: [Domain] }
         to: { namespaces: ["Microsoft.EntityFrameworkCore.**"] }
   ```

3. Add one test that checks the pack:

   ```csharp
   using Ariadne.NET.Testing;

   public class ArchitectureTests
   {
       [Fact]
       public void Solution_follows_its_architecture_rules() => ArchitectureCheck.AssertValid();
   }
   ```

   See [Testing](how-it-works/13-testing.md) for the requirements and for naming the solution explicitly.

4. Translate the ArchUnitNET rules one by one with the [construct map](#construct-map) and [Translating patterns](#translating-patterns).
5. Run both test suites side by side until they agree. Where they do not, check [Things that work differently](#things-that-work-differently) and the catalogue's divergences.
6. Delete the ArchUnitNET tests and remove the `TngTech.ArchUnitNET` package and its test framework adapter, such as `TngTech.ArchUnitNET.xUnit`.

## Construct map

Each table maps ArchUnitNET methods to the YAML that replaces them. The last column names the [catalogue](archunitnet-parity.md) rows the line answers; the catalogue has the status, the parity test and the notes for each. `Ns`, `Name`, `Prefix`, `Suffix` and `Part` stand for your own values. A predicate (`That()`) becomes a selector: the `from` of a `forbid` or `allowOnly` rule or the `for` of a `require` rule. A condition (`Should()`) becomes the rule itself. The negated form of each method is in the [Negation](#negation) table.

### Selecting types

`Types()` and its narrower forms choose which kind of type a rule talks about. In a pack that is the `kinds` key, or `assignableTo` for attribute classes. The kind predicates select the same way.

| ArchUnitNET | Pack YAML | Catalogue id |
|---|---|---|
| `Types()` | any selector; `namespaces: ["**"]` selects every type | — |
| `Classes()` | `kinds: [class, record]`, since a record class is a class to ArchUnitNET | — |
| `Interfaces()` | `kinds: [interface]` | — |
| `Attributes()` | `assignableTo: [System.Attribute]` | — |
| `AreEnums` | `kinds: [enum]` | `p-are-enums` |
| `AreStructs` | `kinds: [struct, recordStruct]` | `p-are-structs` |
| `AreValueTypes` | `kinds: [struct, recordStruct, enum]` | `p-are-value-types` |
| `AreRecord` on `Classes()` | `kinds: [record]` | `p-are-record` |

### Predicates

Methods of `That()`. Each becomes one key of a selector; several keys in one selector must all match.

| ArchUnitNET | Pack YAML | Catalogue id |
|---|---|---|
| `ResideInNamespace("Ns")` | `namespaces: ["Ns"]`; use `"Ns.**"` instead, which matches `Ns` and every namespace below it, where ArchUnitNET matches `Ns` only | `p-reside-in-namespace` |
| `ResideInAssembly` | `assemblies: [Shop.Domain]`, the simple name | `p-reside-in-assembly` |
| `ResideInNamespaceMatching`, `ResideInAssemblyMatching` | no regex; translate by hand, see [Translating patterns](#translating-patterns) | `p-reside-in-namespace-matching`, `p-reside-in-assembly-matching` |
| `ImplementInterface`, `ImplementAny` | `interfaces: ["Ns.IName"]` | `p-implement-interface` |
| `AreAssignableTo` | `assignableTo: ["Ns.Name"]` | `p-are-assignable-to` |
| `AreNested` | `nested: true` | `p-are-nested` |
| `AreNestedIn(Outer)` | `nested: true` with `types: ["Ns.Outer.*"]` | `p-are-nested-in` |
| `AreAbstract` on `Classes()` | `kinds: [class, record]` with `abstract: true`; a static class is not abstract | `p-are-abstract` |
| `AreSealed` on `Classes()` | `kinds: [class, record]` with `sealed: true` | `p-are-sealed` |
| `AreAbstract`, `AreSealed` on `Attributes()` | `assignableTo: [System.Attribute]` with `abstract` or `sealed` | `p-attribute-are-abstract`, `p-attribute-are-sealed` |
| `Are(type)` | `types: ["Ns.Name"]` | `p-are` |
| `ArePublic` | `accessibility: [public]` | `p-are-public` |
| `AreInternal` | `accessibility: [internal]` | `p-are-internal` |
| `AreProtected` | `accessibility: [protected]` | `p-are-protected` |
| `ArePrivate` | `accessibility: [private]` | `p-are-private` |
| `AreProtectedInternal` | `accessibility: [protected internal]` | `p-are-protected-internal` |
| `ArePrivateProtected` | `accessibility: [private protected]` | `p-are-private-protected` |
| `HaveName("Name")` | `names: ["Name"]` or `types: ["**.Name"]` | `p-have-name` |
| `HaveNameStartingWith("Prefix")` | `names: ["Prefix*"]` or `types: ["**.Prefix*"]` | `p-have-name-starting-with` |
| `HaveNameEndingWith("Suffix")` | `names: ["*Suffix"]` or `types: ["**.*Suffix"]` | `p-have-name-ending-with` |
| `HaveNameContaining("Part")` | `names: ["*Part*"]` or `types: ["**.*Part*"]` | `p-have-name-containing` |
| `HaveFullName("Ns.Name")` | `types: ["Ns.Name"]`, a nested type joined with `.` | `p-have-full-name` |
| `HaveFullNameStartingWith("Ns.Prefix")` | `types: ["Ns.Prefix*", "Ns.Prefix*.**"]` | `p-have-full-name-starting-with` |
| `HaveFullNameEndingWith("Suffix")` | `types: ["**.*Suffix"]` | `p-have-full-name-ending-with` |
| `HaveFullNameContaining("Part")` | `types: ["**.*Part*.**"]` | `p-have-full-name-containing` |
| `HaveNameMatching`, `HaveFullNameMatching` | no regex; translate by hand, see [Translating patterns](#translating-patterns) | `p-have-name-matching`, `p-have-full-name-matching` |
| `HaveAssemblyQualifiedName` and its variants | `assemblies` plus `types` in one selector | `p-have-assembly-qualified-name` |
| `HaveAnyAttributes` | `attributes: ["Ns.AuditedAttribute"]`, the full name with `Attribute`; `DoNotHaveAnyAttributes` has no form yet, see #97 | `p-have-any-attributes` |

### Conditions

Methods of `Should()`. Placement and shape become a `require` rule; the keys `is` and `isNot` take every selector key except `layers` and `members`, and each key is checked on its own. Dependencies become `forbid`, `allowOnly` or a layer.

| ArchUnitNET | Pack YAML | Catalogue id |
|---|---|---|
| `ResideInNamespace`, `ResideInAssembly` | `require: { for: …, layer: Domain }`, with a layer that selects by `namespaces` or `assemblies` | `c-reside-in-namespace`, `c-reside-in-assembly` |
| `ResideInNamespaceMatching`, `ResideInAssemblyMatching` | no regex; translate by hand, see [Translating patterns](#translating-patterns) | `c-reside-in-namespace-matching`, `c-reside-in-assembly-matching` |
| `ImplementInterface`, `ImplementAny`, `ImplementAnyInterfacesThat` | `require: { for: …, implements: { types: ["Ns.IName"] } }` | `c-implement-interface` |
| `BeAssignableTo`, `BeAssignableToTypesThat` | `require: { for: …, is: { assignableTo: ["Ns.Name"] } }` | `c-be-assignable-to` |
| `BeEnums` | `is: { kinds: [enum] }` | `c-be-enums` |
| `BeStructs` | `is: { kinds: [struct, recordStruct] }` | `c-be-structs` |
| `BeValueTypes` | `is: { kinds: [struct, recordStruct, enum] }` | `c-be-value-types` |
| `BeNested` | `is: { nested: true }` | `c-be-nested` |
| `BeNestedIn(Outer)` | `is: { nested: true, types: ["Ns.Outer.*"] }` | `c-be-nested-in` |
| none: 0.13.2 has no type-level `BeStatic` | `is: { static: true }` | `c-be-static` |
| `BeAbstract` on `Classes()` | `for: { kinds: [class, record] }` with `is: { abstract: true }` | `c-be-abstract` |
| `BeSealed` on `Classes()` | `for: { kinds: [class, record] }` with `is: { sealed: true }` | `c-be-sealed` |
| `BeRecord` on `Classes()` | `for: { kinds: [class, record] }` with `is: { kinds: [record] }` | `c-be-record` |
| `BeAbstract`, `BeSealed` on `Attributes()` | `for: { assignableTo: [System.Attribute] }` with `is: { abstract: true }` or `is: { sealed: true }` | `c-attribute-be-abstract`, `c-attribute-be-sealed` |
| `Be`, `BeTypesThat` | `is: { types: ["Ns.Name"] }` | `c-be` |
| `Be(Classes())`, `Be(Interfaces())` | `is: { kinds: [class, record] }`, `is: { kinds: [interface] }`; the older `kind: class` leaves out records | `c-be-kind` |
| `BePublic` | `is: { accessibility: [public] }`; the older `accessibility: public` checks nothing | `c-be-public` |
| `BeInternal` | `accessibility: internal` or `is: { accessibility: [internal] }` | `c-be-internal` |
| `BeProtected` | `is: { accessibility: [protected] }` | `c-be-protected` |
| `BePrivate` | `is: { accessibility: [private] }` | `c-be-private` |
| `BeProtectedInternal` | `is: { accessibility: [protected internal] }` | `c-be-protected-internal` |
| `BePrivateProtected` | `is: { accessibility: [private protected] }` | `c-be-private-protected` |
| `HaveName("Name")` | `is: { names: ["Name"] }` | `c-have-name` |
| `HaveNameStartingWith("Prefix")` | `is: { names: ["Prefix*"] }` | `c-have-name-starting-with` |
| `HaveNameEndingWith("Suffix")` | `is: { names: ["*Suffix"] }` | `c-have-name-ending-with` |
| `HaveNameContaining("Part")` | `is: { names: ["*Part*"] }` | `c-have-name-containing` |
| `HaveFullName("Ns.Name")` | `is: { types: ["Ns.Name"] }` | `c-have-full-name` |
| `HaveFullNameStartingWith("Ns.Prefix")` | `is: { types: ["Ns.Prefix*", "Ns.Prefix*.**"] }` | `c-have-full-name-starting-with` |
| `HaveFullNameEndingWith("Suffix")` | `is: { types: ["**.*Suffix"] }` | `c-have-full-name-ending-with` |
| `HaveFullNameContaining("Part")` | `is: { types: ["**.*Part*.**"] }` | `c-have-full-name-containing` |
| `HaveNameMatching`, `HaveFullNameMatching` | no regex; translate by hand, see [Translating patterns](#translating-patterns) | `c-have-name-matching`, `c-have-full-name-matching` |
| `HaveAssemblyQualifiedName` and its variants | `is` with `assemblies` plus `types` | `c-have-assembly-qualified-name` |
| `HaveAnyAttributes`, `HaveAnyAttributesThat` | `is: { attributes: ["Ns.AuditedAttribute"] }` | `c-have-any-attributes` |
| `OnlyDependOn`, `OnlyDependOnTypesThat` | a layer with `allowedDependencies`; cover the forbidden types with a layer too, see [Things that work differently](#things-that-work-differently) | `c-only-depend-on` |

### Negation

ArchUnitNET negates a method with its `DoNot` or `Not` form. A pack negates in three ways: a predicate's `DoNot` form becomes an exception on the rule, a condition's `Not` form becomes `isNot`, and a dependency that must not exist becomes `forbid`.

| ArchUnitNET | Pack YAML | Catalogue id |
|---|---|---|
| `That().DoNot…`, such as `DoNotResideInNamespace` or `AreNotAssignableTo` | `exceptions: [{ namespaces: ["Ns"], because: "…" }]`, with the key of the positive form; a list trait such as `kinds` or `accessibility` can also list the other values instead. An exception takes only `namespaces`, `assemblies`, `types` and the type traits. `DoNotImplementInterface` uses `assignableTo`. `DoNotHaveAnyAttributes` and the negations of `attributes`, `interfaces` and `baseTypes` have no exception form, see #97 | — |
| `Should().Not…`, such as `NotBeSealed` or `NotHaveName` | `isNot` with the key of the positive form, such as `isNot: { sealed: true }` | — |
| `NotDependOnAny`, `NotDependOnAnyTypesThat` | `forbid: { from: …, to: … }` | `c-not-depend-on-any` |
| `NotExist` | `require` with `isNot: { types: ["**"] }` and `allowEmpty: true` | `c-not-exist` |

### Slices

`Slices().Matching(...)` and `MatchingWithPackages(...)` become a `slices` rule. See [Slice patterns](#slice-patterns) for the pattern.

| ArchUnitNET | Pack YAML | Catalogue id |
|---|---|---|
| `Matching("X.(*)")` with `BeFreeOfCycles` | `slices: { matching: ["X.(**)"], freeOfCycles: true }` | `s-free-of-cycles` |
| `Matching("X.(*)")` with `NotDependOnEachOther` | `slices: { matching: ["X.(**)"], independent: true }` | `s-not-depend-on-each-other` |
| `MatchingWithPackages("X.(*)")` | the same as `Matching`; Ariadne names each slice by the captured text | `s-matching-with-packages` |

### Combinators

A pack has no expression language. The keys of one selector must all match, and the values of one list are alternatives.

| ArchUnitNET | Pack YAML | Catalogue id |
|---|---|---|
| `And()` between predicates | several keys in one selector, such as `{ namespaces: ["Ns.**"], kinds: [class] }` | `x-and` |
| `Or()` between two uses of one predicate | several values in one list, such as `types: ["**.OrderService", "**.PaymentService"]` | `x-or-within-predicate` |
| `Or()` between different predicates | no form; write one rule per branch | `x-or-across-predicates` |
| a negation inside an `Or()` group | no form; split the rule or use exceptions | `x-nested-not` |
| `AndShould()`, `OrShould()` | no form; write one rule per condition. An `OrShould` chain cannot be translated | `x-and-should-or-should` |

### Constructs with no rule

These rows are `deferred` or `out-of-scope` in the catalogue. Keep such a check in C# until a pack can say it, or drop it.

| ArchUnitNET | Pack YAML | Catalogue id |
|---|---|---|
| `Exist` | for a fact that guards the target of another rule, set `requireTarget: true` on that rule, see [Vacuity replaces the "is loaded" facts](#vacuity-replaces-the-is-loaded-facts). Otherwise any rule with that selector as its subject fails as `VacuousRule` when it selects nothing | `c-exist` |
| `DependOnAny` as a condition | none yet; dependency as a requirement, see #96 | `c-depend-on-any` |
| `DependOnAny`, `OnlyDependOn` as a predicate | none yet; dependency as a selector, see #96 | `p-depend-on-any`, `p-only-depend-on` |
| `CallAny`, `NotCallAny` | none yet; member level, see #94. A `forbid` with `members` in `to` already forbids calls to the members it names | `p-call-any`, `c-call-any` |
| `HaveMemberWithName` and its method, property and field forms | none yet; member level, see #94 | `p-have-member-with-name`, `c-have-member-with-name` |
| `AreImmutable`, `BeImmutable` on `Classes()` | none yet; member level, see #94 | `p-are-immutable`, `c-be-immutable` |
| `HaveAttributeWithArguments`, `HaveAttributeWithNamedArguments`, `OnlyHaveAttributes` and their forms | none yet; attribute arguments, see #95 | `p-have-attribute-with-arguments`, `c-have-attribute-with-arguments`, `p-have-attribute-with-named-arguments`, `c-have-attribute-with-named-arguments`, `p-only-have-attributes`, `c-only-have-attributes` |
| `FollowCustomPredicate`, `FollowCustomCondition` | none; a pack cannot run C# code | `p-follow-custom-predicate`, `c-follow-custom-condition` |
| `AdhereToPlantUmlDiagram` | none; layers and `allowedDependencies` say what a component diagram says | `c-adhere-to-plant-uml-diagram` |
| `Members()`: `AreDeclaredIn`, `AreStatic`, `AreReadOnly`, `AreImmutable` and their conditions | none yet; member level, see #94 | `p-member-are-declared-in`, `c-member-be-declared-in`, `p-member-are-static`, `c-member-be-static`, `p-member-are-read-only`, `c-member-be-read-only`, `p-member-are-immutable`, `c-member-be-immutable` |
| `MethodMembers()`: `AreCalledBy`, `AreConstructors`, `AreVirtual`, `HaveDependencyInMethodBodyTo`, `HaveReturnType` and their conditions | none yet; member level, see #94 | `p-method-are-called-by`, `c-method-be-called-by`, `p-method-are-constructors`, `c-method-be-constructor`, `p-method-are-virtual`, `c-method-be-virtual`, `p-method-have-dependency-in-method-body-to`, `c-method-have-dependency-in-method-body-to`, `p-method-have-return-type`, `c-method-have-return-type` |
| `PropertyMembers()`: `AreVirtual`, getters, setters and their accessibility, and their conditions | none yet; member level, see #94 | `p-property-are-virtual`, `c-property-be-virtual`, `p-property-have-accessors`, `c-property-have-accessors`, `p-property-have-accessor-accessibility`, `c-property-have-accessor-accessibility` |

## Translating patterns

ArchUnitNET's `…Matching` methods take a .NET regex. A pack takes globs, so a regex has to be translated by hand. The glob rules are in [Selectors](how-it-works/03-rule-schema.md#selectors): a namespace or type name is split on `.` into segments, `*` matches characters within one segment, and a segment that is exactly `**` matches zero or more segments. Matching is case-sensitive.

ArchUnitNET matches a regex anywhere in the name unless it is anchored with `^` and `$`. Check the anchors before you translate: an unanchored `Dto` also matches `Shop.Dtos.Order`.

### A namespace and everything below it

`ResideInNamespaceMatching(@"^Thalos(\.|$)")` selects the namespace `Thalos` and every namespace below it, but not `ThalosExtras`. The glob is `Thalos.**`, because `**` matches zero or more segments: it matches `Thalos` and `Thalos.Agents`, and not `ThalosExtras`, whose first segment is not `Thalos`. Do not write `Thalos*`, which also matches `ThalosExtras`.

```yaml
schema: 2
name: shop
rules:
  - id: SHOP-NO-THALOS-IN-DOMAIN
    because: The domain does not know the agent framework.
    forbid:
      from: { assemblies: [Shop.Domain] }
      to: { namespaces: ["Thalos.**"] }
```

### An assembly name

Assemblies are matched differently from namespaces. The whole assembly name is one string, and `*` matches any characters, dots included. There are no segments and no `**`. `ResideInAssemblyMatching(@"^Thalos(\.|$)")` selects the assembly `Thalos` and every assembly below it, but not `ThalosExtras`. List both: `assemblies: ["Thalos", "Thalos.*"]`. See [Selectors](how-it-works/03-rule-schema.md#selectors).

### Prefix, suffix and part of a name

`StartingWith`, `EndingWith` and `Containing` become `*` globs. Two name predicates joined with `And()` become one glob: `HaveNameStartingWith("I")` with `HaveNameEndingWith("Repository")` is `names: ["I*Repository"]`.

```yaml
schema: 2
name: shop
layers:
  - name: Domain
    id: SHOP-LAYER-DOMAIN
    because: The domain depends on no other layer.
    assemblies: [Shop.Domain]
rules:
  - id: SHOP-REPOSITORY-INTERFACES-IN-DOMAIN
    because: The domain owns the repository contracts.
    require:
      for: { kinds: [interface], names: ["I*Repository"] }
      layer: Domain
```

`HaveFullNameContaining("Dto")` checks the namespace and the type name. The glob is `types: ["**.*Dto*.**"]`: `*Dto*` is the segment that holds the text, and the `**` on both sides lets it sit anywhere, including the type name itself. `types: ["**.*Dto*"]` checks the type name only, which is `HaveNameContaining`. A part that spans a dot splits at it: `HaveFullNameContaining("Api.Dto")` is `**.*Api.Dto*.**`.

```yaml
schema: 2
name: shop
rules:
  - id: SHOP-NO-DTOS-IN-DOMAIN
    because: Data transfer objects belong to the edges.
    require:
      for: { assemblies: [Shop.Domain] }
      isNot: { types: ["**.*Dto*.**"] }
```

### Alternation

A regex with alternation becomes several values in one list, because the values of a list are alternatives. `ResideInNamespaceMatching(@"^(Rag|Thalos)(\.|$)")` is `namespaces: ["Rag.**", "Thalos.**"]`.

```yaml
schema: 2
name: shop
rules:
  - id: SHOP-NO-AGENT-LIBRARIES-IN-DOMAIN
    because: The domain does not know the agent and retrieval libraries.
    forbid:
      from: { assemblies: [Shop.Domain] }
      to: { namespaces: ["Rag.**", "Thalos.**"] }
```

### What has no glob form

A glob has no character classes, no repetition and no optional parts within a segment. Such a regex has to be widened or listed:

- `^Shop\.V[0-9]+(\.|$)` has a character class. Widen it to `Shop.V*.**`, which also matches `Shop.Vendors`, or list the versions that exist: `["Shop.V1.**", "Shop.V2.**"]`.
- `Services?$` has an optional character. List both: `["**.*Service", "**.*Services"]`.
- A case-insensitive regex has no form, since globs keep case. List the spellings that occur.

When a widened glob selects too much, narrow it with an exception.

### Slice patterns

ArchUnitNET's `Slices().Matching("X.(*)")` and `Matching("X.(**)")` both translate to `X.(**)`. ArchUnitNET makes every full namespace below `X` its own slice, so `X.Orders` and `X.Orders.Internal` are two slices, and Ariadne's `(**)` does the same. `X.(*).**` is not the same rule: it folds `X.Orders.Internal` into slice `Orders`, so `Orders` depending on its own internals is no violation.

ArchUnitNET ignores a suffix after `(*)`, so `X.(*).Core` also becomes `X.(**)`.

```yaml
schema: 2
name: shop
rules:
  - id: SHOP-MODULES-ACYCLIC
    because: Modules form a hierarchy.
    slices:
      matching: ["Shop.Modules.(**)"]
      freeOfCycles: true
```

## Things that work differently

### Failing types

ArchUnitNET reports the objects that fail the condition. Ariadne reports the type that breaks the rule, with the rule's `id` and `because`. A dependency rule gives one violation per source type and target type, located at the first reference, with the number of references. A `require` rule gives one violation per type and failed key. So one ArchUnitNET failure can be several Ariadne violations, and each names what to fix.

### NotExist

`Should().NotExist()` becomes a `require` rule that refuses every type it selects. `allowEmpty: true` keeps the rule from failing as vacuous once the namespace is empty, which is the goal.

```yaml
schema: 2
name: shop
rules:
  - id: SHOP-NO-LEGACY-NAMESPACE
    because: The legacy namespace was removed.
    allowEmpty: true
    require:
      for: { namespaces: ["Shop.Legacy.**"] }
      isNot: { types: ["**"] }
```

### OnlyDependOn

`Should().OnlyDependOn(...)` maps to a layer's `allowedDependencies`. Layers are open-world: a dependency on a type that no layer covers is not checked, while ArchUnitNET fails it. To get ArchUnitNET's answer, cover the types the subject may not use with a layer that is not in `allowedDependencies`. Framework types count for neither tool. See [c-only-depend-on](archunitnet-parity.md#c-only-depend-on).

```yaml
schema: 2
name: shop
layers:
  - name: Domain
    id: SHOP-LAYER-DOMAIN
    because: The domain depends only on its shared kernel.
    namespaces: ["Shop.Domain.**"]
    allowedDependencies: [Kernel]
  - name: Kernel
    id: SHOP-LAYER-KERNEL
    because: The shared kernel depends on nothing.
    namespaces: ["Shop.Kernel.**"]
  - name: Rest
    id: SHOP-LAYER-REST
    because: Covers the other Shop code, so the domain cannot depend on it.
    namespaces: ["Shop.Application.**", "Shop.Infrastructure.**", "Shop.Api.**"]
    allowedDependencies: [Domain, Kernel]
```

### Vacuity replaces the "is loaded" facts

ArchUnitNET suites often have facts that only check that a type or assembly is loaded, so a rule about it does not pass on nothing. Ariadne checks this itself:

- A rule whose subject selects no type fails the run as `VacuousRule`, and a layer that covers no type as `VacuousLayer`.
- A `forbid`, `allowOnly` or `uniqueNames` rule with `requireTarget: true` also fails when its target matches no type declared or referenced in the solution (for `uniqueNames`, declared). Set it where a rename of the target would silently end the rule, such as a single seam to an interface.

`requireTarget` proves that the solution uses the target somewhere, not which project does. A library that the solution reaches only transitively is never found, because the analysis sees the types that code writes; keep such a fact in C#. See [A target that matches nothing](how-it-works/rule-types.md#a-target-that-matches-nothing).

```yaml
schema: 2
name: shop
rules:
  - id: SHOP-ONE-SUBAGENT-SEAM
    because: Subagents start only through the executor, which records the run.
    requireTarget: true
    allowOnly:
      from: { types: ["Shop.Application.Agents.SubagentRunExecutor"] }
      to: { types: ["Thalos.ISubagentRunner"] }
```

### Divergences

Where the two tools answer differently on purpose, the catalogue gives the fixture, the reason and the answer to expect. See [Divergences](archunitnet-parity.md#divergences) for the full list.

- **Records and `Classes()`.** `kind: class` leaves out records, which `Classes()` holds; write `kinds: [class, record]`. See [c-be-kind](archunitnet-parity.md#c-be-kind).
- **Static classes and `AreAbstract`.** ArchUnitNET reads a static class as abstract; Ariadne does not. Select static classes with `static: true`. See [p-are-abstract](archunitnet-parity.md#p-are-abstract).
- **Static classes and `BeAbstract`.** The same difference as a condition. See [c-be-abstract](archunitnet-parity.md#c-be-abstract).
- **`OnlyDependOn` and uncovered types.** A layer does not check a dependency on a type no layer covers. See [c-only-depend-on](archunitnet-parity.md#c-only-depend-on).
- **Names: generic arity.** ArchUnitNET reads `` OrderService`1 ``; Ariadne matches `OrderService`. This changes `HaveName`, `HaveNameEndingWith`, `HaveFullName` and `HaveFullNameEndingWith`. See [Names](archunitnet-parity.md#names-generic-arity-nested-types-and-case).
- **Names: nested types.** ArchUnitNET joins a nested type with `+`, Ariadne with `.`, so a full name is `Outer.Inner`. See [Names](archunitnet-parity.md#names-generic-arity-nested-types-and-case).
- **Names: case.** ArchUnitNET ignores case in `HaveName` and the `HaveFullName` methods; Ariadne always keeps case. See [Names](archunitnet-parity.md#names-generic-arity-nested-types-and-case).

## Worked example: Daedalus.NET

This section translates the architecture tests of [Daedalus.NET](https://github.com/MarcelRoozekrans/Daedalus.NET/tree/1dd470b7) at commit `1dd470b7` into one pack. Daedalus is a Clean Architecture solution with an agent adapter on top of [Thalos.NET](https://github.com/MarcelRoozekrans/Thalos.NET). The example is pinned to that commit; Phase 4.6 switches Daedalus to the pack and brings this section up to date.

The tests come from four files:

- `tests/Daedalus.Tests.Unit/Architecture/CleanArchitectureTests.cs`, 34 facts, most of them ArchUnitNET rules;
- `tests/Daedalus.Tests.Unit/Architecture/NoEntityFrameworkTests.cs`, a scan of the restored package graphs;
- `tests/Daedalus.Tests.Unit/Architecture/NuGetPackageRootPathTests.cs`, a text scan of MSBuild files;
- `tests/Daedalus.Tests.Integration/Architecture/CleanArchitectureTests.cs`, a check of the running DI container.

### The test map

Every fact has one row. The Pack rule cell names the rules and layers of the [`daedalus` pack](#the-translated-pack) that replace it, or says `stays in C#` or `not needed`, with the reason.

| Daedalus test | Pack rule | Notes |
|---|---|---|
| `DomainLayer_ShouldNotDependOn_ApplicationLayer` | `DAE-LAYER-DOMAIN` | Domain has no `allowedDependencies`. |
| `DomainLayer_ShouldNotDependOn_InfrastructureLayer` | `DAE-LAYER-DOMAIN` | As above. |
| `DomainLayer_ShouldNotDependOn_ApiLayer` | `DAE-LAYER-DOMAIN` | As above. |
| `ApplicationLayer_ShouldNotDependOn_InfrastructureLayer` | `DAE-LAYER-APPLICATION` | Application may depend on Domain only. |
| `ApplicationLayer_ShouldNotDependOn_ApiLayer` | `DAE-LAYER-APPLICATION` | As above. |
| `InfrastructureLayer_ShouldNotDependOn_ApiLayer` | `DAE-LAYER-INFRASTRUCTURE` | Infrastructure may depend on Domain and Application. |
| `DomainLayer_ShouldNotDependOn_Thalos` | `DAE-NO-THALOS-IN-DOMAIN` | `^Thalos(\.\|$)` becomes `Thalos.**`. |
| `DomainLayer_ShouldNotDependOn_ZeroAllocOrm` | `DAE-NO-ORM-IN-DOMAIN` | `^ZeroAlloc\.ORM(\.\|$)` becomes `ZeroAlloc.ORM.**`. |
| `DomainLayer_ShouldNotDependOn_ZeroAllocOutbox` | `DAE-NO-OUTBOX-IN-DOMAIN` | `^ZeroAlloc\.Outbox(\.\|$)` becomes `ZeroAlloc.Outbox.**`. |
| `DomainLayer_ShouldNotDependOn_Cronos` | `DAE-NO-CRONOS-IN-DOMAIN` | `^Cronos(\.\|$)` becomes `Cronos.**`. |
| `OnlySubagentRunExecutor_DependsOn_ISubagentRunner` | `DAE-ONE-SUBAGENT-SEAM` | `NotExist` over `DependOnAny` with ten `DoNotHaveFullName` exclusions becomes one `allowOnly` with the ten types as `from`. The fact was scoped to the solution's own assemblies; Ariadne checks only the solution's code, and an exception exempts the test projects, which ArchUnitNET did not load. |
| `SubagentRunnerType_IsLoaded_SoTheSingleSeamRuleCoversIt` | `DAE-ONE-SUBAGENT-SEAM` | `requireTarget: true`: the run fails when no Daedalus code references `Thalos.ISubagentRunner`. `SubagentRunExecutor` and nine other types in `Daedalus.Agents` do. |
| `DomainLayer_ShouldNotDependOn_AgentsProject` | `DAE-LAYER-DOMAIN` | As the other Domain layer facts. |
| `ApplicationLayer_ShouldNotDependOn_Thalos` | `DAE-NO-THALOS-IN-APPLICATION` | |
| `ApplicationLayer_ShouldNotDependOn_AgentsProject` | `DAE-LAYER-APPLICATION` | |
| `AgentsProject_ShouldNotDependOn_ApiLayer` | `DAE-LAYER-AGENTS` | Agents may depend on Domain, Application and Infrastructure. |
| `Infrastructure_ShouldNotDependOn_Thalos` | `DAE-NO-THALOS-IN-INFRASTRUCTURE` | |
| `WebLayer_ShouldNotDependOn_Thalos` | `DAE-NO-THALOS-IN-WEB` | |
| `WebLayer_KeepsNoCopyOf_AnApplicationDto` | `DAE-WEB-NO-DTO-COPIES` | A reflection test becomes a `uniqueNames` rule. Both compare simple names with generic arity, so `PagedResultDto<T>` collides only with a generic type of the same name. |
| `ApplicationDtoNames_CoverEveryDtoNamespace_TheNoCopyRuleIsLoadBearing` | `DAE-WEB-NO-DTO-COPIES` | The fact tested the C# helper `ApplicationDtoNames()`, which goes away. `against` is `Daedalus.Application.DTOs.**`, which covers the root namespace and every sub-namespace, and `requireTarget: true` fails the run when no type is declared there. The subject side, the Web types, is checked for vacuity on every run. |
| `AgentsProject_DoesDependOn_Thalos_RulesAreLoadBearing` | `DAE-NO-THALOS-IN-DOMAIN`, `DAE-NO-THALOS-IN-APPLICATION`, `DAE-NO-THALOS-IN-INFRASTRUCTURE`, `DAE-NO-THALOS-IN-WEB` | Each sets `requireTarget: true`, so the run fails when no Daedalus code references a `Thalos.**` type. This proves Thalos is used somewhere, not that `Daedalus.Agents` uses it. That is enough here: the four rules flag every other layer that references Thalos. |
| `OnlyAgentsProject_DependsOn_RagNet` | `DAE-RAGNET-ONLY-IN-AGENTS` | One `forbid` from the five layers the fact loops over. No `requireTarget`: see the next row. |
| `MemoryRagNetAdapter_DoesDependOn_RagNet_RuleIsLoadBearing` | stays in C# | Daedalus reaches Rag.NET only through `Thalos.NET.Memory.RagNet`, and no Daedalus code names a `Rag.**` type, so `requireTarget` could never find the target. Keep a reflection check that `typeof(IVectorStore).Namespace` still starts with `Rag.`. |
| `Endpoints_ShouldResideIn_ApiLayer` | `DAE-ENDPOINTS-IN-API` | `Classes()` holds records, so `kinds` is `[class, record]`. |
| `RepositoryInterfaces_ShouldResideIn_ApplicationLayer` | `DAE-REPOSITORY-INTERFACES-IN-APPLICATION` | `HaveNameStartingWith("I").And().HaveNameEndingWith("Repository")` becomes `names: ["I*Repository"]`. |
| `RepositoryImplementations_ShouldResideIn_InfrastructureLayer` | `DAE-REPOSITORIES-IN-INFRASTRUCTURE` | `AreNotAbstract()` becomes `abstract: false`, plus `static: false`, because ArchUnitNET reads a static class as abstract. Test doubles such as `RacingProjectRepository` live in the test projects, which ArchUnitNET did not load; an exception exempts them. |
| `SkillsAssembly_IsLoaded_SoTheThalosRulesCoverIt` | not needed | The fact made ArchUnitNET load `Thalos.NET.Skills`. Ariadne reads every type the code references from the compilation, so there is nothing to load. No rule targets `Thalos.Skills` on its own; the Thalos rules cover it through `Thalos.**`. |
| `ChannelsAssemblies_AreLoaded_SoTheThalosRulesCoverThem` | not needed | The same for `Thalos.NET.Channels` and `Thalos.NET.Channels.Telegram`. |
| `ZeroAllocOrmAssembly_IsLoaded_SoTheDomainRuleCoversIt` | `DAE-NO-ORM-IN-DOMAIN` | `requireTarget: true`. `Daedalus.Infrastructure` and `Daedalus.Agents` use `ZeroAlloc.ORM`. |
| `ZeroAllocOutboxAssembly_IsLoaded_SoTheDomainRuleCoversIt` | `DAE-NO-OUTBOX-IN-DOMAIN` | `requireTarget: true`. `Daedalus.Agents` uses `ZeroAlloc.Outbox`, for example in `RunSteps`. |
| `CronosAssembly_IsLoaded_SoTheDomainRuleCoversIt` | `DAE-NO-CRONOS-IN-DOMAIN` | `requireTarget: true`. `ScheduleReconciler` and `ScheduledRunStore` in `Daedalus.Agents` use `Cronos`. |
| `No_project_references_ZeroAlloc_Scheduling` | `DAE-NO-ZEROALLOC-SCHEDULING` | A `.csproj` text scan becomes `bannedPackages`. The pattern matches any package id that contains the name, as the scan did. The rule also catches the package when it arrives transitively. |
| `FluentValidation_is_absent_from_every_project_and_from_central_package_management` | `DAE-NO-FLUENTVALIDATION` | `bannedPackages` reads the restored package graph of each project. An entry in `Directory.Packages.props` that no project references is not covered; keep a text check if that matters. |
| `CSharpFunctionalExtensions_is_absent_from_every_project_and_from_central_package_management` | `DAE-NO-CSHARPFUNCTIONALEXTENSIONS` | As above: `Directory.Packages.props` entries that no project references are not covered. |
| `No_project_resolves_entity_framework` | `DAE-NO-ENTITY-FRAMEWORK` | From `NoEntityFrameworkTests.cs`. The fact read `obj/project.assets.json`, and so does `bannedPackages`; the projects must be restored. The prefix `Microsoft.EntityFrameworkCore` becomes `Microsoft.EntityFrameworkCore.**`. |
| `Every_path_built_on_NuGetPackageRoot_ensures_its_trailing_slash` | stays in C# | From `NuGetPackageRootPathTests.cs`. A text scan of `.csproj`, `.props` and `.targets` files; no pack rule reads MSBuild text. |
| `Every_validated_dto_has_a_registered_validation_adapter` | stays in C# | From the integration project. It needs the DI container the API host builds; a pack sees types and references, not registrations at run time. |

The layer facts become four layers with `allowedDependencies`. A layer forbids every layer it does not list, so the pack is a little stricter than the facts: for example, Infrastructure may not depend on Agents or Web either. Daedalus's project references already rule those out. The API and Web layers have no facts of their own; they are in the pack so the other layers' lists can name them, and they list every layer their project can reach.

### The translated pack

One standalone schema 2 pack, layers selected by assembly, rule ids prefixed `DAE-`. Each `because` is taken from the fact's `Because` text or assertion message, merged where several facts become one layer.

```yaml title=".architecture/rules.yaml"
schema: 2
name: daedalus

layers:
  - name: Domain
    id: DAE-LAYER-DOMAIN
    because: Domain layer must be independent of the Application, Infrastructure and API layers; Daedalus.Agents is an adapter on top of Domain, never the other way round.
    assemblies: [Daedalus.Domain]
    allowedDependencies: []
  - name: Application
    id: DAE-LAYER-APPLICATION
    because: Application layer should only depend on Domain, not on Infrastructure or the API layer; Application defines the DTOs the Agents adapter maps to, not vice versa.
    assemblies: [Daedalus.Application]
    allowedDependencies: [Domain]
  - name: Infrastructure
    id: DAE-LAYER-INFRASTRUCTURE
    because: Infrastructure layer should not depend on API layer.
    assemblies: [Daedalus.Infrastructure]
    allowedDependencies: [Domain, Application]
  - name: Agents
    id: DAE-LAYER-AGENTS
    because: The API composes Daedalus.Agents; the adapter must not reach back into the API host.
    assemblies: [Daedalus.Agents]
    allowedDependencies: [Domain, Application, Infrastructure]
  - name: Api
    id: DAE-LAYER-API
    because: The API host composes every other layer.
    assemblies: [Daedalus.Api]
    allowedDependencies: [Domain, Application, Infrastructure, Agents]
  - name: Web
    id: DAE-LAYER-WEB
    because: The WASM client builds on the Application DTOs.
    assemblies: [Daedalus.Web]
    allowedDependencies: [Domain, Application]

rules:
  # Thalos lives only in Daedalus.Agents and the API.
  - id: DAE-NO-THALOS-IN-DOMAIN
    because: Domain must not know the agent framework.
    requireTarget: true
    forbid:
      from: { layers: [Domain] }
      to: { namespaces: ["Thalos.**"] }
  - id: DAE-NO-THALOS-IN-APPLICATION
    because: Application DTOs are shared with the WASM client and must stay Thalos-free.
    requireTarget: true
    forbid:
      from: { layers: [Application] }
      to: { namespaces: ["Thalos.**"] }
  - id: DAE-NO-THALOS-IN-INFRASTRUCTURE
    because: Infrastructure stays free of Thalos; Daedalus.Agents is the one project that bridges the two.
    requireTarget: true
    forbid:
      from: { layers: [Infrastructure] }
      to: { namespaces: ["Thalos.**"] }
  - id: DAE-NO-THALOS-IN-WEB
    because: The WASM client talks to the agent API through Application DTOs only.
    requireTarget: true
    forbid:
      from: { layers: [Web] }
      to: { namespaces: ["Thalos.**"] }

  # Frameworks Domain must not know.
  - id: DAE-NO-ORM-IN-DOMAIN
    because: Domain entities must stay persistence-ignorant; ZeroAlloc.ORM maps live in Infrastructure.
    requireTarget: true
    forbid:
      from: { layers: [Domain] }
      to: { namespaces: ["ZeroAlloc.ORM.**"] }
  - id: DAE-NO-OUTBOX-IN-DOMAIN
    because: Domain must stay ignorant of the outbox transport; step messages live in Daedalus.Agents.
    requireTarget: true
    forbid:
      from: { layers: [Domain] }
      to: { namespaces: ["ZeroAlloc.Outbox.**"] }
  - id: DAE-NO-CRONOS-IN-DOMAIN
    because: Domain must not parse cron expressions; that framework concern lives in Daedalus.Agents.
    requireTarget: true
    forbid:
      from: { layers: [Domain] }
      to: { namespaces: ["Cronos.**"] }

  # One seam to the LLM.
  - id: DAE-ONE-SUBAGENT-SEAM
    because: SubagentRunExecutor, WorkflowNodeDispatcherFactory, BudgetedSubagentRunner, ReviewLensRunner, StandingInstructionsRunner, RunModeRunner, ReviewFindingsRunner, NodeSelectionRunner, WorkIntentRunner and FailedNodeUsageRunner are the only Daedalus types permitted to depend on Thalos' ISubagentRunner; every other caller must go through ISubagentRunExecutor instead.
    requireTarget: true
    allowOnly:
      from:
        types:
          - Daedalus.Agents.Scheduling.SubagentRunExecutor
          - Daedalus.Agents.Workflow.WorkflowNodeDispatcherFactory
          - Daedalus.Agents.Workflow.BudgetedSubagentRunner
          - Daedalus.Agents.Workflow.ReviewLensRunner
          - Daedalus.Agents.Workflow.StandingInstructionsRunner
          - Daedalus.Agents.Workflow.RunModeRunner
          - Daedalus.Agents.Workflow.ReviewFindingsRunner
          - Daedalus.Agents.Workflow.NodeSelectionRunner
          - Daedalus.Agents.Workflow.WorkIntentRunner
          - Daedalus.Agents.Workflow.FailedNodeUsageRunner
      to: { types: [Thalos.ISubagentRunner] }
    exceptions:
      - assemblies: ["Daedalus.Tests.*"]
        because: The rule covers the solution's production code; tests substitute the runner.

  # Rag.NET is an implementation detail of Daedalus.Agents.
  - id: DAE-RAGNET-ONLY-IN-AGENTS
    because: No layer but Daedalus.Agents may know the vector store; Rag.NET lives behind Thalos' IMemoryIndex in Daedalus.Agents.
    forbid:
      from: { layers: [Domain, Application, Infrastructure, Api, Web] }
      to: { namespaces: ["Rag.**"] }

  # Where types live.
  - id: DAE-ENDPOINTS-IN-API
    because: Endpoints are API-layer concerns.
    require:
      for: { names: ["*Endpoints"], kinds: [class, record] }
      layer: Api
  - id: DAE-REPOSITORY-INTERFACES-IN-APPLICATION
    because: Repository contracts belong in Application layer.
    require:
      for: { names: ["I*Repository"], kinds: [interface] }
      layer: Application
  - id: DAE-REPOSITORIES-IN-INFRASTRUCTURE
    because: Repository implementations belong in Infrastructure layer.
    require:
      for: { names: ["*Repository"], kinds: [class, record], abstract: false, static: false }
      layer: Infrastructure
    exceptions:
      - assemblies: ["Daedalus.Tests.*"]
        because: Test doubles live next to the tests.

  # The Web binds the Application DTOs and keeps no copy of its own.
  - id: DAE-WEB-NO-DTO-COPIES
    because: The Web binds Daedalus.Application.DTOs instead of copying them.
    requireTarget: true
    uniqueNames:
      for: { layers: [Web] }
      against: { namespaces: ["Daedalus.Application.DTOs.**"] }

  # Packages that must not come back.
  - id: DAE-NO-ZEROALLOC-SCHEDULING
    because: ZeroAlloc.Scheduling is excluded by design; ScheduledRuns.NextRunAt is the source of truth and the sweep is idempotent, so durable job state buys nothing over the plain BackgroundService.
    bannedPackages:
      packages: ["**.*ZeroAlloc.Scheduling*.**"]
  - id: DAE-NO-FLUENTVALIDATION
    because: Phase 1.7 replaced FluentValidation with ZeroAlloc.Validation.
    bannedPackages:
      packages: ["**.*FluentValidation*.**"]
  - id: DAE-NO-CSHARPFUNCTIONALEXTENSIONS
    because: Phase 1.7 migrated every caller off CSharpFunctionalExtensions; it must not return.
    bannedPackages:
      packages: ["**.*CSharpFunctionalExtensions*.**"]
  - id: DAE-NO-ENTITY-FRAMEWORK
    because: Phase 3.5 removed EF Core; no project may resolve it again, directly or transitively.
    bannedPackages:
      packages:
        - "Microsoft.EntityFrameworkCore.**"
        - "Npgsql.EntityFrameworkCore.PostgreSQL"
        - "ZeroAlloc.Outbox.EfCore"
        - "ZeroAlloc.ValueObjects.EfCore"
```

The three facts that stay in C# remain tests. Without ArchUnitNET, the Rag.NET fact becomes a plain reflection assertion that `typeof(IVectorStore).Namespace` starts with `Rag.`.

### Before and after

#### A layer fact

```csharp
[Fact]
public void DomainLayer_ShouldNotDependOn_ApplicationLayer()
{
    var rule = Types().That().Are(DomainTypes)
        .Should().NotDependOnAny(ApplicationTypes)
        .Because("Domain layer must be independent of Application layer");

    rule.Check(Architecture);
}
```

`DomainTypes` and `ApplicationTypes` select by assembly, so the layers do too. One layer list replaces every `X_ShouldNotDependOn_Y` fact for its layer.

```yaml
schema: 2
name: daedalus-layers
layers:
  - name: Domain
    id: DAE-LAYER-DOMAIN
    because: Domain layer must be independent of Application layer.
    assemblies: [Daedalus.Domain]
    allowedDependencies: []
  - name: Application
    id: DAE-LAYER-APPLICATION
    because: Application layer should only depend on Domain.
    assemblies: [Daedalus.Application]
    allowedDependencies: [Domain]
```

#### A namespace pattern

```csharp
private const string ThalosNamespacePattern = "^Thalos(\\.|$)";

[Fact]
public void DomainLayer_ShouldNotDependOn_Thalos()
{
    var rule = Types().That().Are(DomainTypes)
        .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(ThalosNamespacePattern)
        .Because("Domain must not know the agent framework");

    rule.Check(Architecture);
}
```

The anchored regex becomes `Thalos.**`, which matches `Thalos` and every namespace below it, but not `ThalosX`. `requireTarget: true` replaces the `AgentsProject_DoesDependOn_Thalos_RulesAreLoadBearing` fact.

```yaml
schema: 2
name: daedalus-thalos
layers:
  - name: Domain
    id: DAE-LAYER-DOMAIN
    because: Domain depends on no other layer.
    assemblies: [Daedalus.Domain]
rules:
  - id: DAE-NO-THALOS-IN-DOMAIN
    because: Domain must not know the agent framework.
    requireTarget: true
    forbid:
      from: { layers: [Domain] }
      to: { namespaces: ["Thalos.**"] }
```

#### The single seam

```csharp
[Fact]
public void OnlySubagentRunExecutor_DependsOn_ISubagentRunner()
{
    var rule = Types().That().Are(DaedalusOwnTypes).And().DependOnAny(SubagentRunnerType)
        .And().DoNotHaveFullName(typeof(SubagentRunExecutor).FullName!)
        .And().DoNotHaveFullName(typeof(WorkflowNodeDispatcherFactory).FullName!)
        // ... eight more exclusions ...
        .Should().NotExist()
        .Because("SubagentRunExecutor, ... are the only Daedalus types permitted to depend on Thalos' ISubagentRunner");

    rule.Check(Architecture);
}
```

"Every type that depends on X, except these, must not exist" is an `allowOnly`: the excluded types become `from`, and X becomes `to`. `requireTarget: true` replaces `SubagentRunnerType_IsLoaded_SoTheSingleSeamRuleCoversIt`.

```yaml
schema: 2
name: daedalus-seam
rules:
  - id: DAE-ONE-SUBAGENT-SEAM
    because: Only the executor and its decorators may depend on Thalos' ISubagentRunner; every other caller goes through ISubagentRunExecutor.
    requireTarget: true
    allowOnly:
      from:
        types:
          - Daedalus.Agents.Scheduling.SubagentRunExecutor
          - Daedalus.Agents.Workflow.WorkflowNodeDispatcherFactory
          # ... eight more types ...
      to: { types: [Thalos.ISubagentRunner] }
    exceptions:
      - assemblies: ["Daedalus.Tests.*"]
        because: The rule covers the solution's production code; tests substitute the runner.
```

#### A placement rule with AreNotAbstract

```csharp
[Fact]
public void RepositoryImplementations_ShouldResideIn_InfrastructureLayer()
{
    var rule = Classes().That().HaveNameEndingWith("Repository")
        .And().AreNotAbstract()
        .Should().ResideInAssembly(InfrastructureAssembly)
        .Because("Repository implementations belong in Infrastructure layer");

    rule.Check(Architecture);
}
```

`ResideInAssembly` becomes a `require` with `layer`. `Classes()` holds records, so `kinds` lists both. ArchUnitNET reads a static class as abstract, so `static: false` keeps static classes out, as `AreNotAbstract()` did.

```yaml
schema: 2
name: daedalus-repositories
layers:
  - name: Infrastructure
    id: DAE-LAYER-INFRASTRUCTURE
    because: Infrastructure depends on no other layer in this example.
    assemblies: [Daedalus.Infrastructure]
rules:
  - id: DAE-REPOSITORIES-IN-INFRASTRUCTURE
    because: Repository implementations belong in Infrastructure layer.
    require:
      for: { names: ["*Repository"], kinds: [class, record], abstract: false, static: false }
      layer: Infrastructure
    exceptions:
      - assemblies: ["Daedalus.Tests.*"]
        because: Test doubles live next to the tests.
```
