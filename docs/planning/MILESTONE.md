# Milestone 4: ArchUnitNET parity

**Status:** active
**Started:** 2026-11-11

## Goal

Anyone who writes architecture rules in ArchUnitNET can write the same rules in Ariadne and get the same verdicts. A docs-site catalogue maps each ArchUnitNET type-level rule kind to its Ariadne equivalent; a harness in this repo runs both tools side by side on the same fixture variants and fails CI when they disagree. Ariadne gains the type-level conditions and slice rules it lacks, a migration guide shows how to move over, and Daedalus, the first real consumer, replaces its ArchUnitNET tests with an Ariadne pack. Ariadne itself depends on nothing from Daedalus. See `docs/superpowers/specs/2026-11-11-milestone-4-design.md`.

## Definition of Done

- [ ] All planned phases complete
- [ ] All tests passing
- [ ] The docs site has a parity catalogue listing every type-level and slice rule kind of the pinned ArchUnitNET version, each with its Ariadne equivalent or marked deferred (member level) or out of scope (custom predicates), with the reason
- [ ] Every in-scope catalogue entry has a clean and a broken fixture variant; CI runs ArchUnitNET and Ariadne on both and they agree on verdict and offending types, except where a `Divergences` entry with a fixture and a reason covers it; `KnownGaps` holds no type-level or slice entry; every `Divergences` entry appears in the catalogue
- [ ] Packs express type-level conditions (naming pattern, base type, attribute, sealed, abstract, static, nested) and slice rules (no cycles, no dependency between slices); the pack JSON Schema and its conformance tests cover them
- [ ] The built-in `modular-monolith` pack forbids one module's types from depending on another module's internals, a fixture proves it, and #32 is closed
- [ ] A migration guide on the docs site maps each ArchUnitNET construct to pack YAML and translates Daedalus's rules as a worked example
- [ ] Daedalus `main` references no ArchUnitNET package; its CI checks an Ariadne pack through `Ariadne.NET.Testing`; the only architecture tests left in C# are `NuGetPackageRootPathTests` and the DI integration test
- [ ] Member-level rule kinds are filed as one issue

## Phases

1. Phase 4.1 — Parity catalogue and harness [complete]
2. Phase 4.2 — Type-level conditions [complete]
3. Phase 4.3 — Slices [complete]
4. Phase 4.4 — Target vacuity [complete]
5. Phase 4.5 — Migration guide [complete]
6. Phase 4.6 — Daedalus switch-over [complete]

## Audit History

| Date | Verdict | Gaps |
|---|---|---|
