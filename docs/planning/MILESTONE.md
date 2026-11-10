# Milestone 2: Rule engine v2

**Status:** complete
**Started:** 2026-11-04
**Completed:** 2026-11-10

## Goal

A v2 pack can state every rule in the roadmap design's Daedalus appendix, and the engine enforces each one against the real type graph of a solution instead of its `using` lines. A rule says which types it covers through namespace and assembly patterns, carries a `because` that every violation repeats, and fails when it covers nothing. Packs can also wall off sensitive APIs per layer, such as SQL, crypto or HTTP access outside Infrastructure. The CLI and the MCP server run the same engine. See `docs/superpowers/specs/2026-11-04-milestone-2-design.md`.

## Definition of Done

- [x] All planned phases complete
- [x] All tests passing
- [x] Every rule type in the roadmap design's Daedalus appendix is expressible in a v2 pack and enforced: the fixture solution holds one rule of each type, the clean variant passes and the broken variant fails with that rule's id and `because`
- [x] Dependency checks run on the semantic type graph of a `Compilation`; `SourceValidator` is deleted and CLI `validate` and MCP `validate_source` use the new engine; issue #1 is closed
- [x] `Ariadne.NET.Workspaces` loads a `.sln`, `.slnx` or `.csproj` through MSBuildWorkspace and is packed and published by CI beside the other two packages
- [x] A rule or layer selector that matches no type fails, unless the rule sets `allowEmpty: true` (subject selectors only: `from` and `for`)
- [x] A pack can forbid namespaces, types and members per layer, and the pack reference shows SQL, crypto and HTTP access confined to Infrastructure as a worked example
- [x] The v2 loader rejects unknown keys and rules without a `because`
- [x] All 10 built-in packs and `.architecture/rules.yaml` are on schema v2, and this repo passes its own pack in CI
- [x] A JSON Schema for v2 is in the repo and on the docs site, and the docs-site pack reference documents every rule type
- [x] The milestone's changes ship to nuget.org in a release-please release

## Phases

1. Phase 2.1 — Semantic dependency analysis [complete]
2. Phase 2.2 — Rule schema v2 [complete]
3. Phase 2.3 — Enforce placement and code-feature rules [complete]
4. Phase 2.4 — Banned packages, forbidden APIs and name collisions [complete]
5. Phase 2.5 — Vacuity detection [complete]
6. Phase 2.6 — Pack reference and JSON Schema [complete]

## Audit History

| Date | Verdict | Gaps |
|---|---|---|
| 2026-11-10 | FAIL | Pack reference showed SQL only; crypto and HTTP confinement to Infrastructure was not documented. |
| 2026-11-10 | PASS | Gap closed by #60. See `docs/superpowers/reviews/2026-11-10-milestone-2-audit.md`. |
