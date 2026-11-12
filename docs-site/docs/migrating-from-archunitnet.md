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

2. Create the pack. `ariadne init` writes `.architecture/rules.yaml` from a built-in template, or write it by hand. A minimal pack with two layers and one rule:

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
| `ResideInNamespace("Ns")` | `namespaces: ["Ns"]`; add `"Ns.**"` to include child namespaces, which ArchUnitNET leaves out | `p-reside-in-namespace` |
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

Methods of `Should()`. Placement and shape become a `require` rule; the keys `is` and `isNot` take any selector key except `layers`, and each key is checked on its own. Dependencies become `forbid`, `allowOnly` or a layer.

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
| no type-level form | `is: { static: true }` | `c-be-static` |
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
| `That().DoNot…`, such as `DoNotResideInNamespace` or `AreNotAssignableTo` | `exceptions: [{ namespaces: ["Ns"], because: "…" }]`, with the key of the positive form; a list trait such as `kinds` or `accessibility` can also list the other values instead | — |
| `Should().Not…`, such as `NotBeSealed` or `NotHaveName` | `isNot` with the key of the positive form, such as `isNot: { sealed: true }` | — |
| `NotDependOnAny`, `NotDependOnAnyTypesThat` | `forbid: { from: …, to: … }` | `c-not-depend-on-any` |
| `NotExist` | `require` with `isNot: { types: ["**"] }` and `allowEmpty: true` | `c-not-exist` |

### Slices

`Slices().Matching(...)` and `MatchingWithPackages(...)` become a `slices` rule. See [Translating patterns](#slice-patterns) for the pattern.

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
| `Exist` | none needed: every rule fails as `VacuousRule` when its subject selects nothing | `c-exist` |
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
- A `forbid`, `allowOnly` or `uniqueNames` rule with `requireTarget: true` also fails when its target matches no type declared or referenced in the solution. Set it where a rename of the target would silently end the rule, such as a single seam to an interface.

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
