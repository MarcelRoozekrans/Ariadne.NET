---
sidebar_position: 6
title: ArchUnitNET parity
---

# ArchUnitNET parity

This page lists every type-level and slice rule kind of [ArchUnitNET](https://github.com/TNG/ArchUnitNET) and says how a pack expresses it, or why it does not. The version is pinned at `TngTech.ArchUnitNET` 0.13.2; the rows come from that package's public API. Member-level kinds are listed too, as `deferred`.

The test project `tests/Ariadne.NET.Parity.Tests` checks the page. For each row in scope it runs the ArchUnitNET rule and the Ariadne pack on one small fixture project and compares the types each tool flags. A row is `covered` only when its harness case passes in CI.

How the comparison works:

- Only types declared in source are compared. ArchUnitNET reads compiled IL, so it also sees types the compiler generates, such as closure classes for lambdas and state machines for `async` methods. Those types, whose metadata names contain `<` or which carry `CompilerGeneratedAttribute`, are left out of ArchUnitNET's result. Ariadne never sees them.
- Each tool reports the type that breaks the rule. For a dependency rule that is the type doing the depending.
- A predicate (`That()`) cannot fail on its own, so its case pairs it with `Should().NotDependOnAny(...)` against a pack `forbid` rule.

ArchUnitNET's `Because("...")` maps to a rule's `because`, which every Ariadne rule requires. It needs no row.

Each row has one of these statuses:

| Status | Meaning |
|--------|---------|
| `covered` | A pack expresses it today and the harness case agrees with ArchUnitNET. |
| `gap` | In scope, but a pack cannot express it yet. The notes name the phase that adds it. The harness already runs the ArchUnitNET side. |
| `divergence` | Both tools express it but answer differently on purpose, for example because IL and source differ. See [Divergences](#divergences). |
| `deferred` | Not part of this comparison yet. The notes link the issue that tracks it. |
| `out-of-scope` | Ariadne will not add it. The notes give the reason. |
| `ariadne-only` | Ariadne has it and ArchUnitNET 0.13.2 does not. |

A row pairs a positive method with its negation. The negation is the row's `:not` case. The Ariadne column names the [rule types](how-it-works/rule-types.md) and [selector keys](how-it-works/03-rule-schema.md#selectors) a pack uses. Keys written as `is`, `isNot`, `names`, `kinds`, `assignableTo`, `sealed`, `abstract`, `static` and `nested`, and an `accessibility` list in a selector, do not exist yet; phase 4.2 adds them.

## Type predicates

Methods of `Types().That()`.

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `p-reside-in-namespace` | `ResideInNamespace`, `DoNotResideInNamespace` | `namespaces`; `:not` as an exception with `namespaces` | `covered` | The namespace alone, without `.**`: ArchUnitNET does not include child namespaces. |
| `p-reside-in-assembly` | `ResideInAssembly`, `DoNotResideInAssembly` | `assemblies`; `:not` as an exception with `assemblies` | `covered` | `assemblies` takes the simple name; ArchUnitNET's string form takes the full display name, with version and culture. |
| `p-reside-in-namespace-matching` | `ResideInNamespaceMatching`, `DoNotResideInNamespaceMatching` | — | `out-of-scope` | Ariadne patterns are globs; translate a regex with a glob form by hand. |
| `p-reside-in-assembly-matching` | `ResideInAssemblyMatching`, `DoNotResideInAssemblyMatching` | — | `out-of-scope` | Ariadne patterns are globs; translate a regex with a glob form by hand. |
| `p-implement-interface` | `ImplementInterface`, `ImplementAny`, `DoNotImplementInterface`, `DoNotImplementAny` | `interfaces`; `:not` as an exception with `assignableTo` | `gap` | 4.2, `:not` only |
| `p-are-assignable-to` | `AreAssignableTo`, `AreNotAssignableTo` | `assignableTo`; `:not` as an exception with `assignableTo` | `gap` | 4.2 |
| `p-are-enums` | `AreEnums`, `AreNotEnums` | `kinds: [enum]` | `gap` | 4.2 |
| `p-are-structs` | `AreStructs`, `AreNotStructs` | `kinds: [struct, recordStruct]` | `gap` | 4.2 |
| `p-are-value-types` | `AreValueTypes`, `AreNotValueTypes` | `kinds: [struct, recordStruct, enum]` | `gap` | 4.2 |
| `p-are-nested` | `AreNested`, `AreNotNested` | `nested: true`, `nested: false` | `gap` | 4.2 |
| `p-are-nested-in` | `AreNestedIn` | `nested: true` with `types` under the outer type | `gap` | 4.2 |
| `p-have-member-with-name` | `HaveMemberWithName`, `HaveMethodMemberWithName`, `HavePropertyMemberWithName`, `HaveFieldMemberWithName`, `DoNotHaveMemberWithName`, `DoNotHaveMethodMemberWithName`, `DoNotHavePropertyMemberWithName`, `DoNotHaveFieldMemberWithName` | — | `deferred` | member level, see #94 |

## Type conditions

Methods of `Types().Should()`.

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `c-reside-in-namespace` | `ResideInNamespace`, `NotResideInNamespace` | `require` with `layer`, a layer selecting by `namespaces`; `:not` as `isNot: { namespaces }` | `gap` | 4.2, `:not` only |
| `c-reside-in-assembly` | `ResideInAssembly`, `NotResideInAssembly` | `require` with `layer`, a layer selecting by `assemblies`; `:not` as `isNot: { assemblies }` | `gap` | 4.2, `:not` only |
| `c-reside-in-namespace-matching` | `ResideInNamespaceMatching`, `NotResideInNamespaceMatching` | — | `out-of-scope` | Ariadne patterns are globs; translate a regex with a glob form by hand. |
| `c-reside-in-assembly-matching` | `ResideInAssemblyMatching`, `NotResideInAssemblyMatching` | — | `out-of-scope` | Ariadne patterns are globs; translate a regex with a glob form by hand. |
| `c-implement-interface` | `ImplementInterface`, `ImplementAny`, `ImplementAnyInterfacesThat`, `NotImplementInterface`, `NotImplementAny`, `NotImplementAnyInterfacesThat` | `require` with `implements`; `:not` as `isNot: { interfaces }` | `gap` | 4.2, `:not` only |
| `c-be-assignable-to` | `BeAssignableTo`, `BeAssignableToTypesThat`, `NotBeAssignableTo`, `NotBeAssignableToTypesThat` | `require` with `is: { assignableTo }`, `isNot: { assignableTo }` | `gap` | 4.2 |
| `c-be-enums` | `BeEnums`, `NotBeEnums` | `require` with `is: { kinds: [enum] }`, `isNot` | `gap` | 4.2 |
| `c-be-structs` | `BeStructs`, `NotBeStructs` | `require` with `is: { kinds: [struct, recordStruct] }`, `isNot` | `gap` | 4.2 |
| `c-be-value-types` | `BeValueTypes`, `NotBeValueTypes` | `require` with `is: { kinds: [struct, recordStruct, enum] }`, `isNot` | `gap` | 4.2 |
| `c-be-nested` | `BeNested`, `NotBeNested` | `require` with `is: { nested: true }`, `isNot` | `gap` | 4.2 |
| `c-be-nested-in` | `BeNestedIn` | `require` with `is: { nested: true, types }` | `gap` | 4.2 |
| `c-have-member-with-name` | `HaveMemberWithName`, `HaveMethodMemberWithName`, `HavePropertyMemberWithName`, `HaveFieldMemberWithName`, `NotHaveMemberWithName`, `NotHaveMethodMemberWithName`, `NotHavePropertyMemberWithName`, `NotHaveFieldMemberWithName` | — | `deferred` | member level, see #94 |
| `c-adhere-to-plant-uml-diagram` | `AdhereToPlantUmlDiagram` | — | `out-of-scope` | Ariadne rules are YAML, not diagrams. A pack's layers and `allowedDependencies` say what a component diagram says. |
| `c-be-static` | — | `require` with `is: { static: true }` | `ariadne-only` | ArchUnitNET 0.13.2 has no type-level static; added in 4.2 |

## Class

Methods of `Classes().That()` and `Classes().Should()`.

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `p-are-abstract` | `AreAbstract`, `AreNotAbstract` | `abstract: true`, `abstract: false` | `gap` | 4.2 |
| `c-be-abstract` | `BeAbstract`, `NotBeAbstract` | `require` with `is: { abstract: true }`, `isNot` | `gap` | 4.2 |
| `p-are-sealed` | `AreSealed`, `AreNotSealed` | `sealed: true`, `sealed: false` | `gap` | 4.2 |
| `c-be-sealed` | `BeSealed`, `NotBeSealed` | `require` with `is: { sealed: true }`, `isNot` | `gap` | 4.2 |
| `p-are-record` | `AreRecord`, `AreNotRecord` | `kinds: [record]` | `gap` | 4.2 |
| `c-be-record` | `BeRecord`, `NotBeRecord` | `require` with `is: { kinds: [record] }`, `isNot` | `gap` | 4.2 |
| `p-are-immutable` | `AreImmutable`, `AreNotImmutable` | — | `deferred` | member level, see #94 |
| `c-be-immutable` | `BeImmutable`, `NotBeImmutable` | — | `deferred` | member level, see #94 |

## Attribute

Methods of `Attributes().That()` and `Attributes().Should()`, which select attribute classes.

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `p-attribute-are-abstract` | `AreAbstract`, `AreNotAbstract` | `assignableTo: [System.Attribute]` with `abstract` | `gap` | 4.2 |
| `c-attribute-be-abstract` | `BeAbstract`, `NotBeAbstract` | `require` for `assignableTo: [System.Attribute]` with `is: { abstract: true }`, `isNot` | `gap` | 4.2 |
| `p-attribute-are-sealed` | `AreSealed`, `AreNotSealed` | `assignableTo: [System.Attribute]` with `sealed` | `gap` | 4.2 |
| `c-attribute-be-sealed` | `BeSealed`, `NotBeSealed` | `require` for `assignableTo: [System.Attribute]` with `is: { sealed: true }`, `isNot` | `gap` | 4.2 |

## Object-level

Methods that every kind of object has, here applied to `Types()`.

### Identity and kind

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `p-are` | `Are`, `AreNot` | `types`; `:not` as an exception with `types` | `covered` | |
| `c-be` | `Be`, `BeTypesThat`, `NotBe` | `require` with `is: { types }`, `isNot: { types }` | `gap` | 4.2 |
| `c-be-kind` | `Be(Classes())`, `Be(Interfaces())`, `NotBe(Interfaces())` | `require` with `kind`; `:not` as `isNot: { kinds }` | `gap` | 4.2, `:not` only. `kind: class` leaves out records, which `Classes()` holds; see [c-be-kind](#c-be-kind). |
| `c-exist` | `Exist` | `VacuousRule` | `out-of-scope` | Every Ariadne rule already fails when its subject selects nothing. That failure names no type, so there is nothing to compare. |
| `c-not-exist` | `NotExist` | `require` with `isNot: { types: ["**"] }` and `allowEmpty: true` | `gap` | 4.2 |

### Accessibility

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `p-are-public` | `ArePublic`, `AreNotPublic` | `accessibility: [public]` | `gap` | 4.2 |
| `p-are-internal` | `AreInternal`, `AreNotInternal` | `accessibility: [internal]` | `gap` | 4.2 |
| `p-are-protected` | `AreProtected`, `AreNotProtected` | `accessibility: [protected]` | `gap` | 4.2 |
| `p-are-private` | `ArePrivate`, `AreNotPrivate` | `accessibility: [private]` | `gap` | 4.2 |
| `p-are-protected-internal` | `AreProtectedInternal`, `AreNotProtectedInternal` | `accessibility: [protected internal]` | `gap` | 4.2 |
| `p-are-private-protected` | `ArePrivateProtected`, `AreNotPrivateProtected` | `accessibility: [private protected]` | `gap` | 4.2 |
| `c-be-public` | `BePublic`, `NotBePublic` | `require` with `is: { accessibility: [public] }`, `isNot` | `gap` | 4.2. Today's `require` accepts `accessibility: public` but checks only `internal`, so it flags nothing. |
| `c-be-internal` | `BeInternal`, `NotBeInternal` | `require` with `accessibility: internal`; `:not` as `isNot: { accessibility: [internal] }` | `gap` | 4.2, `:not` only |
| `c-be-protected` | `BeProtected`, `NotBeProtected` | `require` with `is: { accessibility: [protected] }`, `isNot` | `gap` | 4.2 |
| `c-be-private` | `BePrivate`, `NotBePrivate` | `require` with `is: { accessibility: [private] }`, `isNot` | `gap` | 4.2 |
| `c-be-protected-internal` | `BeProtectedInternal`, `NotBeProtectedInternal` | `require` with `is: { accessibility: [protected internal] }`, `isNot` | `gap` | 4.2 |
| `c-be-private-protected` | `BePrivateProtected`, `NotBePrivateProtected` | `require` with `is: { accessibility: [private protected] }`, `isNot` | `gap` | 4.2 |

### Names

`Name` is the simple name and `FullName` the namespace-qualified name. An Ariadne `types` pattern matches the full name segment by segment, so a simple-name pattern starts with `**.`.

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `p-have-name` | `HaveName`, `DoNotHaveName` | `types: ["**.Name"]`; `:not` as an exception with `types` | `covered` | |
| `p-have-name-starting-with` | `HaveNameStartingWith`, `DoNotHaveNameStartingWith` | `types: ["**.Prefix*"]`; `:not` as an exception with `types` | `covered` | |
| `p-have-name-ending-with` | `HaveNameEndingWith`, `DoNotHaveNameEndingWith` | `types: ["**.*Suffix"]`; `:not` as an exception with `types` | `covered` | |
| `p-have-name-containing` | `HaveNameContaining`, `DoNotHaveNameContaining` | `types: ["**.*Part*"]`; `:not` as an exception with `types` | `covered` | |
| `p-have-name-matching` | `HaveNameMatching`, `DoNotHaveNameMatching` | — | `out-of-scope` | Ariadne patterns are globs; translate a regex with a glob form by hand. |
| `p-have-full-name` | `HaveFullName`, `DoNotHaveFullName` | `types: ["Ns.Name"]`; `:not` as an exception with `types` | `covered` | |
| `p-have-full-name-starting-with` | `HaveFullNameStartingWith`, `DoNotHaveFullNameStartingWith` | `types: ["Ns.Prefix*", "Ns.Prefix*.**"]`; `:not` as an exception with `types` | `covered` | |
| `p-have-full-name-ending-with` | `HaveFullNameEndingWith`, `DoNotHaveFullNameEndingWith` | `types: ["**.*Suffix"]`; `:not` as an exception with `types` | `covered` | |
| `p-have-full-name-containing` | `HaveFullNameContaining`, `DoNotHaveFullNameContaining` | `types: ["**.*Part*.**"]`; `:not` as an exception with `types` | `covered` | |
| `p-have-full-name-matching` | `HaveFullNameMatching`, `DoNotHaveFullNameMatching` | — | `out-of-scope` | Ariadne patterns are globs; translate a regex with a glob form by hand. |
| `p-have-assembly-qualified-name` | `HaveAssemblyQualifiedName`, `HaveAssemblyQualifiedNameStartingWith`, `HaveAssemblyQualifiedNameEndingWith`, `HaveAssemblyQualifiedNameContaining`, `HaveAssemblyQualifiedNameMatching` and their `DoNot` forms | — | `out-of-scope` | `assemblies` plus `types` express it. |
| `c-have-name` | `HaveName`, `NotHaveName` | `require` with `is: { names }`, `isNot: { names }` | `gap` | 4.2 |
| `c-have-name-starting-with` | `HaveNameStartingWith`, `NotHaveNameStartingWith` | `require` with `is: { names: ["Prefix*"] }`, `isNot` | `gap` | 4.2 |
| `c-have-name-ending-with` | `HaveNameEndingWith`, `NotHaveNameEndingWith` | `require` with `is: { names: ["*Suffix"] }`, `isNot` | `gap` | 4.2 |
| `c-have-name-containing` | `HaveNameContaining`, `NotHaveNameContaining` | `require` with `is: { names: ["*Part*"] }`, `isNot` | `gap` | 4.2 |
| `c-have-name-matching` | `HaveNameMatching`, `NotHaveNameMatching` | — | `out-of-scope` | Ariadne patterns are globs; translate a regex with a glob form by hand. |
| `c-have-full-name` | `HaveFullName`, `NotHaveFullName` | `require` with `is: { types }`, `isNot: { types }` | `gap` | 4.2 |
| `c-have-full-name-starting-with` | `HaveFullNameStartingWith`, `NotHaveFullNameStartingWith` | `require` with `is: { types: ["Ns.Prefix*", "Ns.Prefix*.**"] }`, `isNot` | `gap` | 4.2 |
| `c-have-full-name-ending-with` | `HaveFullNameEndingWith`, `NotHaveFullNameEndingWith` | `require` with `is: { types: ["**.*Suffix"] }`, `isNot` | `gap` | 4.2 |
| `c-have-full-name-containing` | `HaveFullNameContaining`, `NotHaveFullNameContaining` | `require` with `is: { types: ["**.*Part*.**"] }`, `isNot` | `gap` | 4.2 |
| `c-have-full-name-matching` | `HaveFullNameMatching`, `NotHaveFullNameMatching` | — | `out-of-scope` | Ariadne patterns are globs; translate a regex with a glob form by hand. |
| `c-have-assembly-qualified-name` | `HaveAssemblyQualifiedName`, `HaveAssemblyQualifiedNameStartingWith`, `HaveAssemblyQualifiedNameEndingWith`, `HaveAssemblyQualifiedNameContaining`, `HaveAssemblyQualifiedNameMatching` and their `Not` forms | — | `out-of-scope` | `assemblies` plus `types` express it. |

### Dependencies

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `c-not-depend-on-any` | `NotDependOnAny`, `NotDependOnAnyTypesThat` | `forbid` | `covered` | |
| `c-only-depend-on` | `OnlyDependOn`, `OnlyDependOnTypesThat` | a layer's `allowedDependencies` | `divergence` | Framework types count for neither tool, though ArchUnitNET names `System.Object` in its message. See [c-only-depend-on](#c-only-depend-on). |
| `c-depend-on-any` | `DependOnAny`, `DependOnAnyTypesThat` | — | `deferred` | dependency as a requirement, see #96 |
| `p-depend-on-any` | `DependOnAny`, `DoNotDependOnAny` | — | `deferred` | dependency as a selector, see #96 |
| `p-only-depend-on` | `OnlyDependOn` | — | `deferred` | dependency as a selector, see #96 |
| `p-call-any` | `CallAny`, `DoNotCallAny` | — | `deferred` | member level, see #94 |
| `c-call-any` | `CallAny`, `NotCallAny` | — | `deferred` | member level, see #94 |

### Attributes

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `p-have-any-attributes` | `HaveAnyAttributes`, `DoNotHaveAnyAttributes` | `attributes`; `:not` as an exception with `attributes` | `gap` | 4.2, `:not` only |
| `c-have-any-attributes` | `HaveAnyAttributes`, `HaveAnyAttributesThat`, `NotHaveAnyAttributes`, `NotHaveAnyAttributesThat` | `require` with `is: { attributes }`, `isNot: { attributes }` | `gap` | 4.2 |
| `p-have-attribute-with-arguments` | `HaveAttributeWithArguments`, `HaveAnyAttributesWithArguments`, `DoNotHaveAttributeWithArguments`, `DoNotHaveAnyAttributesWithArguments` | — | `deferred` | attribute arguments, see #95 |
| `c-have-attribute-with-arguments` | `HaveAttributeWithArguments`, `HaveAnyAttributesWithArguments`, `NotHaveAttributeWithArguments`, `NotHaveAnyAttributesWithArguments` | — | `deferred` | attribute arguments, see #95 |
| `p-have-attribute-with-named-arguments` | `HaveAttributeWithNamedArguments`, `HaveAnyAttributesWithNamedArguments`, `DoNotHaveAttributeWithNamedArguments`, `DoNotHaveAnyAttributesWithNamedArguments` | — | `deferred` | attribute arguments, see #95 |
| `c-have-attribute-with-named-arguments` | `HaveAttributeWithNamedArguments`, `HaveAnyAttributesWithNamedArguments`, `NotHaveAttributeWithNamedArguments`, `NotHaveAnyAttributesWithNamedArguments` | — | `deferred` | attribute arguments, see #95 |
| `p-only-have-attributes` | `OnlyHaveAttributes` | — | `deferred` | attribute arguments, see #95 |
| `c-only-have-attributes` | `OnlyHaveAttributes`, `OnlyHaveAttributesThat` | — | `deferred` | attribute arguments, see #95 |

### Custom rules

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `p-follow-custom-predicate` | `FollowCustomPredicate` | — | `out-of-scope` | Rules stay YAML only; a pack cannot run C# code. |
| `c-follow-custom-condition` | `FollowCustomCondition` | — | `out-of-scope` | Rules stay YAML only; a pack cannot run C# code. |

## Slices

`Slices().Matching(...)` and `Slices().MatchingWithPackages(...)` group types into slices by a namespace pattern; `Should()` then checks the slices against each other. Phase 4.3 adds slice rules to packs.

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `s-free-of-cycles` | `Matching`, `BeFreeOfCycles` | — | `gap` | 4.3 |
| `s-not-depend-on-each-other` | `Matching`, `NotDependOnEachOther` | — | `gap` | 4.3 |
| `s-matching-with-packages` | `MatchingWithPackages` | — | `gap` | 4.3 |

## Member level

Methods of `Members()`, `MethodMembers()`, `PropertyMembers()` and `FieldMembers()`. Field members have no methods of their own in 0.13.2; they use the member and object-level methods.

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `p-member-are-declared-in` | `AreDeclaredIn`, `AreNotDeclaredIn` | — | `deferred` | member level, see #94 |
| `c-member-be-declared-in` | `BeDeclaredIn`, `BeDeclaredInTypesThat`, `NotBeDeclaredIn`, `NotBeDeclaredInTypesThat` | — | `deferred` | member level, see #94 |
| `p-member-are-static` | `AreStatic`, `AreNotStatic` | — | `deferred` | member level, see #94 |
| `c-member-be-static` | `BeStatic`, `NotBeStatic` | — | `deferred` | member level, see #94 |
| `p-member-are-read-only` | `AreReadOnly`, `AreNotReadOnly` | — | `deferred` | member level, see #94 |
| `c-member-be-read-only` | `BeReadOnly`, `NotBeReadOnly` | — | `deferred` | member level, see #94 |
| `p-member-are-immutable` | `AreImmutable`, `AreNotImmutable` | — | `deferred` | member level, see #94 |
| `c-member-be-immutable` | `BeImmutable`, `NotBeImmutable` | — | `deferred` | member level, see #94 |
| `p-method-are-called-by` | `AreCalledBy`, `AreNotCalledBy` | — | `deferred` | member level, see #94 |
| `c-method-be-called-by` | `BeCalledBy`, `NotBeCalledBy` | — | `deferred` | member level, see #94 |
| `p-method-are-constructors` | `AreConstructors`, `AreNoConstructors` | — | `deferred` | member level, see #94 |
| `c-method-be-constructor` | `BeConstructor`, `BeNoConstructor` | — | `deferred` | member level, see #94 |
| `p-method-are-virtual` | `AreVirtual`, `AreNotVirtual` | — | `deferred` | member level, see #94 |
| `c-method-be-virtual` | `BeVirtual`, `NotBeVirtual`, `BeMethodMembersThat` | — | `deferred` | member level, see #94 |
| `p-method-have-dependency-in-method-body-to` | `HaveDependencyInMethodBodyTo`, `DoNotHaveDependencyInMethodBodyTo` | — | `deferred` | member level, see #94 |
| `c-method-have-dependency-in-method-body-to` | `HaveDependencyInMethodBodyTo`, `NotHaveDependencyInMethodBodyTo` | — | `deferred` | member level, see #94 |
| `p-method-have-return-type` | `HaveReturnType`, `DoNotHaveReturnType` | — | `deferred` | member level, see #94 |
| `c-method-have-return-type` | `HaveReturnType`, `NotHaveReturnType` | — | `deferred` | member level, see #94 |
| `p-property-are-virtual` | `AreVirtual`, `AreNotVirtual` | — | `deferred` | member level, see #94 |
| `c-property-be-virtual` | `BeVirtual`, `NotBeVirtual` | — | `deferred` | member level, see #94 |
| `p-property-have-accessors` | `HaveGetter`, `HaveSetter`, `HaveInitOnlySetter`, `HaveNoGetter`, `HaveNoSetter`, `DoNotHaveInitOnlySetter` | — | `deferred` | member level, see #94 |
| `c-property-have-accessors` | `HaveGetter`, `HaveSetter`, `HaveInitOnlySetter`, `NotHaveGetter`, `NotHaveSetter`, `NotHaveInitOnlySetter` | — | `deferred` | member level, see #94 |
| `p-property-have-accessor-accessibility` | `HavePublicGetter`, `HavePublicSetter`, `HaveInternalGetter`, `HaveInternalSetter`, `HaveProtectedGetter`, `HaveProtectedSetter`, `HavePrivateGetter`, `HavePrivateSetter`, `HaveProtectedInternalGetter`, `HaveProtectedInternalSetter`, `HavePrivateProtectedGetter`, `HavePrivateProtectedSetter` and their `DoNot` forms | — | `deferred` | member level, see #94 |
| `c-property-have-accessor-accessibility` | `HavePublicGetter`, `HavePublicSetter`, `HaveInternalGetter`, `HaveInternalSetter`, `HaveProtectedGetter`, `HaveProtectedSetter`, `HavePrivateGetter`, `HavePrivateSetter`, `HaveProtectedInternalGetter`, `HaveProtectedInternalSetter`, `HavePrivateProtectedGetter`, `HavePrivateProtectedSetter` and their `Not` forms | — | `deferred` | member level, see #94 |

## Combinators

ArchUnitNET joins predicates with `And()` and `Or()` and negates them with `Not`-style methods. A pack has no expression language: the keys of one selector must all match, the values of one list are alternatives, and `forbid`, exceptions and `isNot` express negation.

| Id | ArchUnitNET | Ariadne | Status | Notes |
|---|---|---|---|---|
| `x-and` | `And()` between predicates | several keys in one selector | `covered` | |
| `x-or-within-predicate` | `Or()` between two uses of one predicate, such as two `ResideInNamespace` calls | several values in one list | `covered` | |
| `x-or-across-predicates` | `Or()` between different predicates | — | `out-of-scope` | Ariadne adds selector keys, not an expression language. Write one rule per branch. |
| `x-nested-not` | a negation of a combined predicate, such as `DoNotResideInNamespace` inside an `Or()` group | — | `out-of-scope` | Ariadne adds selector keys, not an expression language. |
| `x-and-should-or-should` | `AndShould()`, `OrShould()` | — | `out-of-scope` | One rule has one condition. Write one rule per condition; an `OrShould` chain has no YAML form. |

## Divergences

Here ArchUnitNET and Ariadne answer differently on purpose, mostly because one reads IL and the other source. Each entry names its fixture, the reason and the answer a developer would expect.

### c-be-kind

Fixture `tests/fixtures/archunit-parity/CBeKind`, rule `Should().Be(Classes())` against `require` with `kind: class`. Both tools flag the interface `Broken.IOrderService` and the struct `Broken.Money`. Only Ariadne flags the record `Broken.Ledger`.

- **Reason:** a record class compiles to a class, so ArchUnitNET's `Classes()` holds it. Ariadne reads source, where `record` is a kind of its own, so `kind: class` rejects a record.
- **Expected:** ArchUnitNET's answer, since a record class is a class in C#. Phase 4.2's `is: { kinds: [class, record] }` expresses `Be(Classes())` exactly; today's `kind` takes one kind.

### c-only-depend-on

Fixture `tests/fixtures/archunit-parity/COnlyDependOn`, rule `Should().OnlyDependOn(Types().That().ResideInNamespace("….Target"))` against a `Subject` layer with `allowedDependencies: [Allowed]`. Both tools flag `Broken.OrderService`, which depends on `Forbidden.Mailer` in a layer the subject may not use. Only ArchUnitNET flags `Broken.AuditTrail`, which depends on `Helpers.Clock`, a type no layer covers. Neither flags `Broken.ReportBuilder`, which depends on `System.Text.StringBuilder`.

- **Reason:** `OnlyDependOn` fails a dependency on any type of the loaded assemblies outside the allowed set. The layer check is open-world: it does not check a dependency on a type no layer covers. Framework types are outside the loaded assemblies, so they count for neither tool, although ArchUnitNET 0.13.2 names `System.Object` in its failure message.
- **Expected:** ArchUnitNET's answer, for a developer who wrote `OnlyDependOn`. In a pack, cover the namespace with a layer that is not in `allowedDependencies`, as the fixture does for `Forbidden`, and Ariadne flags the dependency too.
