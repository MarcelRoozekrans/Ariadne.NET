# Milestone 2: Rule engine v2

**Status:** active
**Started:** 2026-11-04

## Goal

A v2 pack can state every rule in the roadmap design's Daedalus appendix, and the engine enforces each one against the real type graph of a solution instead of its `using` lines. A rule says which types it covers through namespace and assembly patterns, carries a `because` that every violation repeats, and fails when it covers nothing. Packs can also wall off sensitive APIs per layer, such as SQL, crypto or HTTP access outside Infrastructure. The CLI and the MCP server run the same engine. See `docs/superpowers/specs/2026-11-04-milestone-2-design.md`.

## Definition of Done

- [ ] All planned phases complete
- [ ] All tests passing
- [ ] Every rule type in the roadmap design's Daedalus appendix is expressible in a v2 pack and enforced: the fixture solution holds one rule of each type, the clean variant passes and the broken variant fails with that rule's id and `because`
- [ ] Dependency checks run on the semantic type graph of a `Compilation`; `SourceValidator` is deleted and CLI `validate` and MCP `validate_source` use the new engine; issue #1 is closed
- [ ] `Ariadne.NET.Workspaces` loads a `.sln`, `.slnx` or `.csproj` through MSBuildWorkspace and is packed and published by CI beside the other two packages
- [ ] A rule or layer selector that matches no type fails, unless the rule sets `allowEmpty: true`
- [ ] A pack can forbid namespaces, types and members per layer, and the pack reference shows SQL, crypto and HTTP access confined to Infrastructure as a worked example
- [ ] The v2 loader rejects unknown keys and rules without a `because`
- [ ] All 10 built-in packs and `.architecture/rules.yaml` are on schema v2, and this repo passes its own pack in CI
- [ ] A JSON Schema for v2 is in the repo and on the docs site, and the docs-site pack reference documents every rule type
- [ ] The milestone's changes ship to nuget.org in a release-please release

## Phases

1. Phase 2.1 — Semantic dependency analysis [complete]
2. Phase 2.2 — Rule schema v2 [pending]
3. Phase 2.3 — Enforce placement and code-feature rules [pending]
4. Phase 2.4 — Banned packages, forbidden APIs and name collisions [pending]
5. Phase 2.5 — Vacuity detection [pending]
6. Phase 2.6 — Pack reference and JSON Schema [pending]

## Audit History

| Date | Verdict | Gaps |
|---|---|---|
