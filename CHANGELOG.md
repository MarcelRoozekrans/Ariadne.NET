# Changelog

## [0.9.1](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.9.0...v0.9.1) (2026-11-14)


### Bug Fixes

* check Razor component code and report it at the .razor file ([#128](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/128)) ([7a212cc](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/7a212ccbf2f9411f8e3dd9ed15fff40a62c3e494))

## [0.9.0](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.8.1...v0.9.0) (2026-11-14)


### ⚠ BREAKING CHANGES

* Ariadne.NET.Workspaces needs Microsoft.Build.Framework 18.7.1, so remove the 17.14.28 line that 0.8 required; the package now keeps MSBuild out of the output itself

### Features

* name each analyzer the compiler cannot load when a project fails to compile ([4eb91b7](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/4eb91b745bee4c6012d3e34f9486211e8bc67a9b))


### Bug Fixes

* Ariadne.NET.Workspaces needs Microsoft.Build.Framework 18.7.1, so remove the 17.14.28 line that 0.8 required; the package now keeps MSBuild out of the output itself ([4eb91b7](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/4eb91b745bee4c6012d3e34f9486211e8bc67a9b))
* load Razor components built by the .NET 11 SDK by moving to Microsoft.CodeAnalysis 5.12.0 ([4eb91b7](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/4eb91b745bee4c6012d3e34f9486211e8bc67a9b))

## [0.8.1](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.8.0...v0.8.1) (2026-11-13)


### Bug Fixes

* load a project whose design-time build has an MSBuild warning ([#119](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/119)) ([4d372a0](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/4d372a0fd9b2b1f30aec16da0a4c90928c6229e3))

## [0.8.0](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.7.1...v0.8.0) (2026-11-13)


### ⚠ BREAKING CHANGES

* the generated RuleDefinitionValidator constructor takes a SliceDefinitionValidator argument as well.
* the generated RuleRequirementValidator constructor takes three TargetSelectorValidator arguments instead of one.

### Features

* add requireTarget to report a rule target that matches nothing ([#106](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/106)) ([2b3a38c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/2b3a38ca46535e2075291e24d37e911bc4aab2bf))
* add slices rules for module isolation ([#104](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/104)) ([beee6ec](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/beee6ec307ad6b6a99f8d247da91981acd16545c))
* add type-level traits and is and isNot conditions ([#102](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/102)) ([89ff570](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/89ff570c7f3f5a1b460bcf1a9e63c1df09ae40e1))


### Bug Fixes

* find slice cycles across compatible target frameworks ([#114](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/114)) ([50900fc](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/50900fcb4844cf7290c2a06c7700c85447cee748)), closes [#113](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/113) [#113](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/113) [#113](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/113)


### Documentation

* add the migration guide from ArchUnitNET ([#107](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/107)) ([7b004bb](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/7b004bbd288b1b3deaf632025755c376573cc456))
* groom milestone 4 — ArchUnitNET parity ([#84](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/84)) ([3d7e351](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/3d7e351466044788559669cf113c40af7f7b556a))


### Tests

* add the ArchUnitNET parity catalogue and harness ([#99](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/99)) ([29dd91d](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/29dd91dec6f22ab21babdc450cacf080c1cb440b))

## [0.7.1](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.7.0...v0.7.1) (2026-11-11)


### Features

* add the Ariadne.NET.Testing package with ArchitectureCheck ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))
* ariadne validate checks every pack under the architecture folder ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))
* ariadne validate writes json and sarif reports ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))
* find the repository root for report paths ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))
* name the pack on each architecture violation ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))
* package the ariadne command line as the Ariadne.NET.Cli dotnet tool ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))
* share the text report between the cli and the testing package ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))
* the stop hook and mcp validation check every pack ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))
* write architecture reports as json ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))
* write architecture reports as sarif ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))
* write reports from the testing package ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))


### Bug Fixes

* print the ariadne version without build metadata ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))


### Documentation

* github actions examples that fail the build on a violation ([1c89aee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1c89aeec4975ee721dd1d2a668624699221cde05))
* upload the example sarif on pull request runs and grant actions read ([#81](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/81)) ([e73f40a](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e73f40aa4d816a79418e8141cf67ca3d2566c16f))

## [0.7.0](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.6.1...v0.7.0) (2026-11-10)


### ⚠ BREAKING CHANGES

* add FindPackageCoverageGap to IArchitectureValidator, so custom implementations must add it

### Features

* add FindPackageCoverageGap to IArchitectureValidator, so custom implementations must add it ([09d30b5](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/09d30b5b99c67dde3c9ced7e77954d77f5c3599e))


### Documentation

* show SQL, crypto and HTTP confined to Infrastructure ([#60](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/60)) ([25b27d6](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/25b27d61c6a85e92264a1d74625648946e5e4781))

## [0.6.1](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.6.0...v0.6.1) (2026-11-09)


### Features

* publish a JSON Schema and a rule type reference for packs ([#58](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/58)) ([024682e](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/024682e41f4adb2b2dff3d7973841ae791da07ec))

## [0.6.0](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.5.0...v0.6.0) (2026-11-08)


### ⚠ BREAKING CHANGES

* a rule, role or layer that selects no type is now an error, set allowEmpty true on a rule or role that may be empty

### Features

* a rule, role or layer that selects no type is now an error, set allowEmpty true on a rule or role that may be empty ([98ed64f](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/98ed64f251d45dcbde569b9fed721fcd1e8ddf95))
* add allowEmpty to rules and component roles ([98ed64f](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/98ed64f251d45dcbde569b9fed721fcd1e8ddf95))
* add the VacuousRule and VacuousLayer violation kinds and the vacuity pass ([98ed64f](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/98ed64f251d45dcbde569b9fed721fcd1e8ddf95))
* show vacuity violations and allowEmpty in CLI, MCP and hooks ([98ed64f](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/98ed64f251d45dcbde569b9fed721fcd1e8ddf95))

## [0.5.0](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.4.1...v0.5.0) (2026-11-08)


### ⚠ BREAKING CHANGES

* **abstractions:** add banned package and unique name rule kinds and member selectors; the generated public RuleDefinitionValidator constructor gained a TargetSelectorValidator parameter for the new Against property

### Features

* **abstractions:** add banned package and unique name rule kinds and member selectors; the generated public RuleDefinitionValidator constructor gained a TargetSelectorValidator parameter for the new Against property ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** ban packages by id from a project's resolved graph ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** compile banned package globs once per run ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** exempt the declaring type and its nested types under allowOnly ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** forbid and confine APIs by member ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** keep indexer uses that implement a renamed interface indexer ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** name the compilation parameter when packages are unknown ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** record constructor initializers and indexers as member uses ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** record member uses in the type graph ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** report types that keep a name from another layer ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** tell same-named types in different assemblies apart ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **cli:** list the new rule kinds and enforce them on this repository ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **rule-loader:** read bannedPackages, uniqueNames and members ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **rule-loader:** reject misplaced exception keys on amended rules ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **workspaces:** read the resolved package graph from project.assets.json ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **workspaces:** treat a missing target framework entry as unknown packages ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))


### Bug Fixes

* **analysis:** let an extension block's own class use its members ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** name the assemblies when two colliding names are equal ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** record the implicit base constructor call as a member use ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** record the implicit base constructor call at the first walked declaration ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **rule-loader:** reject a member pattern without a type segment ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **workspaces:** report an unusable assets file as an error ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **workspaces:** treat a non-object targets entry as unknown packages ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))


### Code Refactoring

* **workspaces:** share the runtime identifier split ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))


### Documentation

* **analysis:** list the bannedPackages and uniqueNames rules in the validator summary ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **perf:** attribute the phase 2.4 cost to phase 2.3 and 2.4 ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **perf:** measure the phase 2.4 cost after the final review fixes ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **perf:** record the phase 2.4 baseline ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **perf:** record the walker allocation investigation ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **plan:** carry amended rule fields in task 2 of the phase 2.4 plan ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **plan:** correct the uniqueNames example and spell constructors as new ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **plan:** design phase 2.4 banned packages, forbidden APIs and name collisions ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **plan:** extend the member use list and write nested types with dots ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **plan:** name indexers by metadata name and note the implicit base constructor limit ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **plan:** plan phase 2.4 banned packages, forbidden APIs and name collisions ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **plan:** qualify the name filter claim for indexers ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **plan:** state who allowOnly exempts for member rules ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **rule-schema:** fix stale exception and rule kind sentences ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* state the member use limits and the RuleDefinitionValidator break ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))


### Tests

* **analysis:** pin one name collision across target frameworks ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **analysis:** pin that unrelated member uses record nothing ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **aot:** cover the bannedPackages and uniqueNames rules in the AOT smoke pack ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **hooks:** expect the phase 2.4 rules on the broken daedalus fixture ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))
* **workspaces:** pin unknown packages for a stale assets file ([bc3d67b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc3d67b2e093cee1cd8d40c85647cbb2167b7202))

## [0.4.1](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.4.0...v0.4.1) (2026-11-08)


### Features

* **analysis:** add LayerProbe to find the layers of an assembly and namespace ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **cli:** add the hook command for Claude Code ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **cli:** install the hooks with init --agent claude ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** add the hook host with wire types and fail-open handling ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** ask before a new file lands outside every layer ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** feed validator violations back when a turn ends ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** give the agent the resolved rules at session start ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))


### Bug Fixes

* **cli:** make init --agent claude fail cleanly and preserve the settings file ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **cli:** read and write the hook protocol as UTF-8 ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **cli:** say when init --agent claude changed nothing ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** bound and test the stop hook's failure paths ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** cap the pre-tool-use question ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** harden the host's stderr write and cover cancellation ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** keep the session context within the cap on every path ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** make the placement hook robust and its gates meaningful ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** record edits without races and find the project root after cd ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** remove every record the stop hook read ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))


### Code Refactoring

* **rules:** move rule text into the engine for reuse by the hooks ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **sync:** make the import name rule public instead of sharing internals ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))


### Documentation

* add design for agent guidance and guardrails via hooks ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* add phases 5.6 and 5.7 for agent hooks and guidance ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* add the phase 5.6 agent hooks implementation plan ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* align hook design with current Ariadne code and roadmap ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* correct the merged state of phases 2.2 and 2.3 ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* describe the hook root resolution and record the flaky test finding ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** bring the state file and hook docs in line with the code ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** document the agent hooks and record the measurements ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* point the state file at the phase 5.6 pull request branch ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* record owner approval of the phase 5.6 plan ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* record that the 5.6 plan can start ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* scope hook design as structural hooks plus Guidance package ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* split hook design between structure and AI.Sentinel enforcement ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))


### Tests

* **cli:** cover the settings commit failure and make cleanup best effort ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **hooks:** expect the Hooks project in the repo pack ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))
* **rules:** cover requirement text and document the describer ([e609dcf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e609dcf5f6e2a9660ce61dbbaa04680d5719b820))

## [0.4.0](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.3.0...v0.4.0) (2026-11-06)


### ⚠ BREAKING CHANGES

* **analysis:** report role violations as ROLE-name, CODE-001 and CODE-002 instead of role/name
* **abstractions:** add the require rule and selector keys, RuleDefinitionValidator takes a RuleRequirementValidator

### Features

* **abstractions:** add the require rule and selector keys, RuleDefinitionValidator takes a RuleRequirementValidator ([918551c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/918551ca3d6b900048797edc71c12c5e38ca5f3d))
* **analysis:** enforce require rules on the type graph ([918551c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/918551ca3d6b900048797edc71c12c5e38ca5f3d))
* **analysis:** report role violations as ROLE-name, CODE-001 and CODE-002 instead of role/name ([918551c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/918551ca3d6b900048797edc71c12c5e38ca5f3d))
* **analysis:** select types by base type, interface and attribute ([918551c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/918551ca3d6b900048797edc71c12c5e38ca5f3d))
* **cli:** show require rules, dogfood placement rules and document them ([918551c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/918551ca3d6b900048797edc71c12c5e38ca5f3d))
* **rule-loader:** read and check the require rule ([918551c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/918551ca3d6b900048797edc71c12c5e38ca5f3d))


### Bug Fixes

* **analysis:** check require rules of code-built packs and compile them once ([918551c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/918551ca3d6b900048797edc71c12c5e38ca5f3d))


### Documentation

* correct the because description for role violations ([918551c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/918551ca3d6b900048797edc71c12c5e38ca5f3d))
* describe implements as interface matching and fix the placement example ([918551c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/918551ca3d6b900048797edc71c12c5e38ca5f3d))
* phase 2.3 placement and code-feature rules design and plan ([#40](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/40)) ([15f6eaa](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/15f6eaa87c0740b83ab8dac36df1c6b988f65d0b))


### Tests

* **abstractions:** cover null entries in every new selector list ([918551c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/918551ca3d6b900048797edc71c12c5e38ca5f3d))
* **analysis:** cover generic base chains and all mixed selector keys ([918551c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/918551ca3d6b900048797edc71c12c5e38ca5f3d))
* **rule-loader:** pin require kind casing and empty layer ([918551c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/918551ca3d6b900048797edc71c12c5e38ca5f3d))

## [0.3.0](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.2.2...v0.3.0) (2026-11-05)


### ⚠ BREAKING CHANGES

* **rule-engine:** IRulePackMerger.Merge returns a Result, MergeWithDiagnostics, MergeResult and MergeWarning are removed, and a locked-rule override now fails instead of warning
* **rule-loader:** packs need schema 2, unknown keys, a missing because and v1 packs fail to load, codeFeatureRules and CodeFeatureRuleDefinition are removed, and MCP get_rules returns the v2 rules list
* **cli:** CLI lint and MCP lint_source are removed, validate checks layers on the semantic type graph
* **cli:** the rules output and MCP get_layers payload follow schema v2

### Features

* **cli:** CLI lint and MCP lint_source are removed, validate checks layers on the semantic type graph ([f1d8a14](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/f1d8a14fd2eb80b37718af2a58e5bfe81375c0b5))
* **cli:** the rules output and MCP get_layers payload follow schema v2 ([f1d8a14](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/f1d8a14fd2eb80b37718af2a58e5bfe81375c0b5))
* rule schema v2 with selectors, forbid and allowOnly rules ([#35](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/35)) ([f1d8a14](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/f1d8a14fd2eb80b37718af2a58e5bfe81375c0b5))
* **rule-engine:** IRulePackMerger.Merge returns a Result, MergeWithDiagnostics, MergeResult and MergeWarning are removed, and a locked-rule override now fails instead of warning ([f1d8a14](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/f1d8a14fd2eb80b37718af2a58e5bfe81375c0b5))
* **rule-loader:** packs need schema 2, unknown keys, a missing because and v1 packs fail to load, codeFeatureRules and CodeFeatureRuleDefinition are removed, and MCP get_rules returns the v2 rules list ([f1d8a14](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/f1d8a14fd2eb80b37718af2a58e5bfe81375c0b5))

## [0.2.2](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.2.1...v0.2.2) (2026-11-05)


### Documentation

* phase 2.2 rule schema v2 design and plan ([#33](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/33)) ([8f066bb](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/8f066bb2a5908f344a97adbb2dd3f666bc72168d))

## [0.2.1](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.2.0...v0.2.1) (2026-11-05)


### Documentation

* list all three 0.2.0 breaking changes in the changelog ([#29](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/29)) ([e6d41c0](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e6d41c07309fe4e32760252672e1442fa8d3ec4f))

## [0.2.0](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.1.1...v0.2.0) (2026-11-04)


### ⚠ BREAKING CHANGES

* validate no longer takes a source directory of .cs files.
* MCP validate_source takes path, a solution or project, in place of sourceDirectory, and returns an ArchitectureReport.
* ISourceValidator, SourceValidator, SourceValidationResult, SourceViolation, IDependencyValidator, DependencyValidator, DependencyViolation and GenerateResult.DependencyViolations are removed. Use IArchitectureValidator, or ISolutionValidator from Ariadne.NET.Workspaces.

### Features

* semantic dependency analysis and Ariadne.NET.Workspaces ([#25](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/25)) ([3f8c48d](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/3f8c48dd45ee69bf6546fb7f86536f5cddd0e626))

## [0.1.1](https://github.com/MarcelRoozekrans/Ariadne.NET/compare/v0.1.0...v0.1.1) (2026-11-04)


### Documentation

* **spec:** phase 2.1 semantic dependency analysis design ([#23](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/23)) ([139c227](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/139c227982c3669c3be509b855eef9941b4aa204))

## 0.1.0 (2026-11-04)


### ⚠ BREAKING CHANGES

* **sync:** SyncResult is replaced by SyncedImport and ImportSyncOutcome, IGitSyncProvider and ISyncOrchestrator return Results, and GitCliSyncProvider is no longer public.
* IRulePackLoader and IRulePackResolver return Result<RulePack, AriadneError>.

### Features

* **abstractions:** add AriadneError for Result-based failures ([0953e13](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/0953e133aeeaf7563e37240a2d4a242e43404d47))
* **abstractions:** reject null rule pack members with validation attributes ([7d7af6d](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/7d7af6d24373bf2d7dcbe37a46dd25ff9446460b))
* **abstractions:** validate rule packs with ZeroAlloc.Validation ([a64cf19](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/a64cf1973bf6df03f74e0495a516fa7c53775699))
* add 7 MCP tools and pack resources with tests ([9c32c8e](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/9c32c8e2927314ba099b70c30b0a3d92b056de8a))
* add accessibility mismatch validation for role-matched types ([b282ccf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/b282ccfad5f3445343009394e81c33246f71af62))
* add arch sync CLI command and pass working directory to resolver ([67cda35](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/67cda35d1299328365210e39214cb00c697d372c))
* add ASP.NET Host.Api with architecture endpoints ([780c7db](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/780c7db961d15e15c1c81d122dffc32b39f0ba80))
* add built-in pack registry, resolver, and 3 YAML packs ([f35d2fc](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/f35d2fc13aed33ef55421fd6304ceca048e532b2))
* add CI/CD with GitVersion, release-please, and NuGet publishing ([99e257d](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/99e257d311db6c42f5a8d1ca2e15b26987172a3e))
* add CLI host with init, generate, validate, rules, and roles commands ([220274e](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/220274e73f6478e6653204441ae7030b696a0418))
* add ddd-tactical, event-driven, vertical-slice, and modular-monolith packs ([55e641c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/55e641ce45ac45c74ba5416289e19144a3e025fc))
* add dependency validation engine ([8d9fbf6](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/8d9fbf69324d9cedc82ad4a862eada1933f33108))
* add DI registration for Core services ([f01db88](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/f01db8862a72d441eebd159b7f1a7c5ad8aaf225))
* add Docusaurus documentation site with Mermaid support ([22aae1a](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/22aae1ae428f854f5ff3944223ca7e360ec5de38))
* add generate orchestrator wiring inference, validation, and code gen ([a2eae57](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/a2eae5785eb68ef9556d34e80583600bd5c99ff5))
* add git CLI sync provider with tag resolution and caching ([4183ac5](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/4183ac51e7b11dd887c937afe6b95fddac004196))
* add import models for git-based sync ([7f83135](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/7f8313505050c7670e66402d01bdf610acbbbbb1))
* add import:// protocol to RulePackResolver and register sync services ([5b487e1](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/5b487e128736005c68d185d42b225ddf175e8000))
* add imports.yaml loader for git sync ([bafd9ea](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bafd9ead1c301b925d69328482304bc77c7816c6))
* add layer inference engine ([7432967](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/7432967d1545386daaa816fc4f53c916d2545160))
* add lint command for namespace-directory mismatch detection ([e1d152d](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e1d152d0cd7ef747dd6ee53ddd1a85be813a8557))
* add locked rules enforcement with merge diagnostics ([e8c07a4](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e8c07a4c2f1ae05c9326bb5900f9e0794999970c))
* add MCP HTTP transport to API host ([fc0240b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/fc0240b496642b77424b350d072223cd6968e1ea))
* add missing interface validation for requiresInterface roles ([200ce36](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/200ce36bf949536b22068ace97ec7138d2daf7f1))
* add onion-architecture, layered, and microservice builtin packs ([af49464](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/af494644372025762cde332b835c461be05b3193))
* add Roslyn-based code generation service driven by rule pack ([5dcc115](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/5dcc115cae84db3232cd67a19813138b02286e76))
* add rule pack merger with layer union and role override semantics ([f2dccbd](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/f2dccbd3d580e6f0da966c0b21625b43d709ecde))
* add rule schema models (RulePack, LayerDefinition, ComponentRoleDefinition) ([43fb90a](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/43fb90ac6aa428bb504c57d35ce89adf9b76708a))
* add semver range parsing and matching for git tag resolution ([27f0847](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/27f0847f98e7dda60d4b74da3e8596c1a077507a))
* add source validation engine with Roslyn-based dependency checking ([b339c7e](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/b339c7ecf4926ce11b464ab20ccb2fdfc3cde7b1))
* add sync orchestrator to coordinate import syncing ([2ae32f5](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/2ae32f5cfd184ad42240160fb695f67c6539155f))
* add type kind validation with suffix-based role matching ([ba14ba6](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/ba14ba677be59afd8459cc273acbb4b9145d651c))
* add YAML rule pack loader with YamlDotNet ([5b9c92d](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/5b9c92d52ea121afdd6569c2d1326b864fe65b9a))
* adopt ZInject for compile-time DI registration ([5001b47](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/5001b47f4a3285f6fa6927d6fb2a0f251ff66d54))
* Ariadne.NET identity, family conventions and publishing ([#5](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/5)) ([ef725d0](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/ef725d0df341c7d711da856c5eb4c19659cc8461))
* implement validate, generate-workspace, CORS, and integration tests ([7325183](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/73251834249468414890ef263d1d4b8616c4c27e))
* instrument pack and sync operations with ZeroAlloc.Telemetry ([9e07dd2](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/9e07dd245bbf94e901ec147206f912b5e3bf3a20))
* scaffold ArchGov.Host.Mcp stdio server with RuleResolutionHelper ([7e27b68](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/7e27b685d39c24096cd78768f1e7e25e1eae71c6))
* scaffold solution with Core, Host.Api, Host.Cli, and test projects ([1d77595](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1d7759576971cbec85ce2b1f29ee816255a9e30e))


### Bug Fixes

* **abstractions:** explain null entries in validation messages ([68a33f1](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/68a33f1fa8e72428875bd52e9e219520d9848c54))
* **cli:** build the sync default paths with the platform separator ([118ba23](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/118ba23b1a29aa063cb2b82c2e098f6938744efb))
* **cli:** cover command construction with the unexpected-exception handler ([3f21968](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/3f2196876e58794e7844624c49b0f6293de9fcb1))
* **cli:** report unexpected exceptions with their stack trace ([4c66281](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/4c66281a4c09d105d46dee7d0fd3c9d3087437ec))
* close remaining exception paths in the pack pipeline ([f6cabee](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/f6cabee9d0a25f41ee82900bc20aa18ca6ff5f8e))
* **deps:** update docusaurus monorepo to v3.10.2 ([#12](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/12)) ([760f366](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/760f3663d3a85f37c32c74f51cbbe5db2ebd7d88))
* **docs:** add @docusaurus/faster required by the v4 future flags ([6590b7d](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/6590b7d2dd1035f1a813f4a3887ff310545470df))
* finish the ariadne rename in the CLI init hint and docs ([2872a0e](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/2872a0ed4924fab62a22a18263e73f0cf038cf39))
* **generate:** use the inferred role name ([6772078](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/6772078445a885b52880e028a7c26f7f40e3af75))
* **loader:** name the failing file and hide CLR types in YAML errors ([243ffc0](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/243ffc089a427ae5cd8929141ea9b06779a0573a))
* **loader:** name the pack or file in validation errors ([713c95b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/713c95b3d8d60ff1c2503731d4f9e45bac211c66))
* **mcp:** register RuleResolutionHelper behind an interface ([085f0e7](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/085f0e7cc4d69ef152c23820e5f97e1bc3b6a609))
* move LayerInferenceStatus enum to its own file (MA0048) ([8ca7e40](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/8ca7e405e1b5a274eb9375fe01b9568b7025786b))
* resolve diagnostics from the analyzer bumps ([67520a2](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/67520a20490af88a0d802f02a9770ddc7cba4da9))
* return meaningful exit codes from the CLI ([5772633](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/5772633f1bc61b33f2a17aad91ef3c14ec19f1a2))
* support composite templates in MCP tool template parameter ([f571992](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/f571992912ac3628b355526330cf6edaad7d5991))
* **sync:** harden cache records and .git path checks ([e0caa46](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e0caa46ec6994fc680a787b57258b5d763c87d25))
* **sync:** parse semver without exceptions ([c705574](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/c70557490526231f7aee173704591bf6978f6af5))
* **sync:** pass git arguments as an argv list and contain import paths ([7128c11](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/7128c11ea466e870f8c511bd9cec01890b4392ef))
* **sync:** refuse symlinks when copying pack files ([5385d41](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/5385d41faeb36c04b373b2dca2712d11d0aa7a77))
* **sync:** tighten import path and credential handling ([9aecd73](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/9aecd73df921e22143f6522ce43b1a9b027af302))
* **telemetry:** follow the Minos package convention and tighten tests ([fd8bbe8](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/fd8bbe85d9d7cce5acd891e8e83d51ba34eb6162))
* write every error message as a full sentence ([45c3389](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/45c3389d2e88a2420bcf87e851c5140ce2a9658c))


### Code Refactoring

* **cli:** parse the init template once ([0a311bf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/0a311bfbc8924fa02250bb93389226ca74c49642))
* extract methods in SourceValidator to satisfy MA0051 method length ([484497c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/484497c7b915ada0d9004f41bc22da6279a043a0))
* **loader:** return a Result from BuiltinPackRegistry.GetPack ([d9e720f](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/d9e720fe7859049f720f380dab2fffbfb5064731))
* **loader:** return Results from TemplateReference ([7a1faf6](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/7a1faf6554b400e92927342367900ec9c6095f73))
* **loader:** share YAML document reading between loaders ([b682ab9](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/b682ab997e3de0507fe41b8da363f50f83de1daa))
* move pack and sync implementations next to their facades ([4864859](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/4864859691918ee2c5b1639ed6791797dba071ef))
* remove duplicate REST API, keep MCP-only HTTP host ([be0cae4](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/be0cae49ae3686498a5e4f1900caadbcb0466e97))
* rename ArchGov projects and namespaces to Ariadne.NET ([a3364e6](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/a3364e6f3b26bbeb036db2d4b5ea5d12f1ac4f2d))
* rename user-visible archgov strings to ariadne ([da1de2f](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/da1de2ff94fa5bd28190e467c2ec231ec3b95435))
* return Results from rule pack loading and resolution ([ba287be](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/ba287be7419961a543525658925ee7cbcca7de4c))
* share the import name rule outside Sync ([8c812e9](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/8c812e99a8f49b4dfd9ac616a1b99113ab1f4df1))
* split Ariadne.NET.Abstractions out of the engine ([57211fa](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/57211faa05b6cb560b810dd2a78ec3618a66cf5b))
* **sync:** return Results from sync and retry git with ZeroAlloc.Resilience ([0f94567](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/0f945679ad64eda23c0bff68e00c7eccfdfe4a56))
* **sync:** return Results from the imports loader and SemVerRange ([78f3d18](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/78f3d18c0a1f5889f8b3d6f0c03d5d2f54126c75))


### Documentation

* absolute license link in the readme ([41e8831](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/41e883111d3edcc307d4fd1f82c7d7766ea473e0))
* add CLAUDE.md with build commands and architecture overview ([d3ce671](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/d3ce671a6ebddff3fc7eaf33229025e37a666343))
* add custom rule validation design document ([0d4d082](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/0d4d0829ee7b5402a31d76b75568a162c22c1a77))
* add custom rule validation implementation plan ([17eec1b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/17eec1b80d4a6135ca66e494f771713dca19cf5b))
* add extensive technical documentation with Mermaid diagrams ([7347433](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/73474332b49379a888219729d7aadc301d38a54f))
* add git-based sync design document ([86daeda](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/86daeda3be617f8d03c30b8909352082997e1e6a))
* add git-based sync implementation plan ([5e9d3ec](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/5e9d3ec6d1f7dba8e7a447a5b217f80a2a64306b))
* add management summary and update README with git sync ([1b72755](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1b7275588a3e124abfab37a78106fe03e88fd6d0))
* add MCP server integration design document ([6481528](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/6481528ad7734bb6f820fdbd6271526ebca96fc9))
* add MCP server integration implementation plan ([f9c2243](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/f9c22436aa17b45628083a75c0a70be85c073e77))
* add MCP server section to README ([9696198](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/96961985f10f70e1dc575bc181f4bafa401a0d21))
* add project README with usage, architecture, and API reference ([38607c6](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/38607c653c5d47e86207e0ce5dad9c82ea38aae1))
* align docs with the final phase 1.3 code ([3f3c4af](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/3f3c4af37495d34826437f77e960920bc87c5c50))
* bring CLAUDE.md in line with the new CI ([2629b1a](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/2629b1ab095124ecca6e15cb0b2e36ae58520a1f))
* correct role-only pack and naming validation notes ([15faefb](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/15faefbd2faff52f7390cc422698d7e63e29879f))
* correct the CodeQL and settings notes in RELEASING.md ([a1f6722](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/a1f6722c51a23e077d3cc1b67b9d7cdf0913301c))
* describe the Ariadne.NET.Abstractions package split ([e88f397](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e88f397bfc300cd6880a0df6746d1abd53d0a165))
* document Results, ZeroAlloc conventions and deferrals ([e1d9439](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/e1d943911067ff3cb19a226ee5fde2e994c7e41c))
* drop a home path from STATE.md ([4e50226](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/4e50226fbb163449ab82b99e830c8d8d43bf093b))
* getting started sample also checks a project ([94d3b7c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/94d3b7c816fe8c266739d5146a23b9dcc74e292d))
* library-first readme with family and zeroalloc sections ([307bcdd](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/307bcdd613a533be23ff40e4e0cedc427c651594))
* mark git sync as done, add tests for edge cases ([26ccc58](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/26ccc58600b9d7b40a7ec003f6ad3fb7e0f1f20c))
* move the docs site to Ariadne.NET and add getting started ([efe430f](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/efe430fc62dc5b4722fdcdc43a1908644229ea1c))
* **plan:** phase 1.1 rename to Ariadne.NET implementation plan ([2532445](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/2532445d31bdca9731a0017a3c2a8377c131aedf))
* **plan:** phase 1.2 package split plan ([0ed4921](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/0ed4921e9d2993eb80299df81ed05cc0a321577a))
* **plan:** phase 1.4 publish plan ([3f06dfa](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/3f06dfa3449884cc3d851a771f4f0cc910f8a618))
* **plans:** phase 1.3 family conventions plan ([5988353](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/5988353ac01ce05ef3fab112e84864d5800c508d))
* **plan:** tick off phase 1.2 tasks ([1e1b353](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/1e1b3531de41650c658f93f822bc6bdb95b68738))
* remove stale REST API and AddArchGovCore references ([8fb408b](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/8fb408b57d73c2938083b07a83c5829bd36a50a8))
* remove stale REST API references from features.md ([68c7838](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/68c783810d619cf3baf11413f1c179be3fdfe0ee))
* rename ArchGov to Ariadne.NET in README, CLAUDE.md and docs ([6c2050e](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/6c2050ea9ae5633b4c48b5489ba3037853e74c38))
* replace the old product name in the docs site ([496ee83](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/496ee836a0c72093f7666a2fddb9f11b430364f9))
* show the Abstractions split on the docs site and give the package its own README ([356fd75](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/356fd752682c0b8a9a1454c027e4ed436a04353f))
* **spec:** adopt ci-baseline v1 in phase 1.4 ([ffc761d](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/ffc761d43ea562addaf1bee92d68ce0821a2d213))
* **spec:** phase 1.1 rename impact analysis ([54394e8](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/54394e8088a618b438eeaa7232ff91fb3e0e8fcd))
* **spec:** phase 1.1 rename to Ariadne.NET design ([c442ed0](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/c442ed03be34ab09b94636dbfd1daf15bed1c7d1))
* **spec:** phase 1.2 package split design ([841b471](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/841b471d0b48fc607396a5806afa1f10ef077a4c))
* **spec:** phase 1.2 package split impact analysis ([165a272](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/165a2727c676823049db8ff043fdbe1bc283d805))
* **spec:** phase 1.4 publish design ([312f771](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/312f77139ea510cbb2f3a7fc7e513aaa94bf742b))
* **specs:** add Ariadne.NET roadmap design ([ad9286d](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/ad9286dd8831dfb03e5c65b6a9b8cbbe50521ed6))
* **specs:** add codebase map ([4ebd251](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/4ebd251294cfe5cea1a061a7a7007e60daa90c3c))
* **specs:** phase 1.3 family conventions design ([b44b151](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/b44b15108d058827cb0e5ab898b13fcdc8c2dd09))
* **specs:** phase 1.3 impact analysis ([acd2ec6](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/acd2ec63b5ef2bde2078bba824f18ba63825011d))
* state what an unreserved prefix risks ([bc7d409](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/bc7d4098f72d365d470a9f6f3eee06ae98f0b19e))
* update design docs to reflect implemented state ([47a5ec0](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/47a5ec0ed5d8e6ec6c438d35463086537e3aaa4e))
* update design document with 10 shipped builtin packs ([6740871](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/674087104931e99ffe11f27ee613311e45ff04ef))
* update README with lint, locked rules, and 4 new built-in packs ([1581852](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/15818522ba8201820276dfee25e97d3def110c74))
* update README with validate, generate-workspace, and updated test counts ([afa3617](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/afa3617e46818ac410759756d3d83a4ec5230489))


### Tests

* **abstractions:** use an explicit dependency allow-list ([0161da3](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/0161da38c070eda8572517d2c8fcbd6486d0914d))
* add integration tests for git sync flow ([376a049](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/376a04944a4f91b23ab0ec96800e2bef77fca5fa))
* add integration tests for source validation with real clean-architecture pack ([4b73ad9](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/4b73ad9b54a40e7f2e267ea55131abe6c191027d))
* add native AOT smoke app for Ariadne.NET.Abstractions ([09a8a66](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/09a8a6620e901140e35e257772cd505b18ca7f2d))
* add per-pack integration tests and dogfooding rules.yaml ([0af1fdf](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/0af1fdf3ca5e690cc648677286065e1223abb328))
* **cli:** expect the cache path with the platform separator ([ad74b72](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/ad74b72b076525f4f1f3d558f5493d3cec7cb58d))
* fill scaffolding and integration test gaps ([7c7ae05](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/7c7ae050ff533d1c0afe532b6f5df5d74dbd58a3))
* **mcp:** verify IRuleResolutionHelper resolves from DI ([b148e2c](https://github.com/MarcelRoozekrans/Ariadne.NET/commit/b148e2c7f2e0cafe791d939ba32480facc6208a6))
