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

## Translating patterns

## Things that work differently

## Worked example: Daedalus.NET
