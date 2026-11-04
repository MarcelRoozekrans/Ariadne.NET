# Milestone 1: Ariadne.NET identity and family shape

**Status:** complete
**Started:** 2026-10-30
**Completed:** 2026-11-04

## Goal

Rename the architecture governance engine to Ariadne.NET and give it the shape of its siblings in the Cretan family (Rag.NET, Minos.NET, Mnemosyne.NET): separate Abstractions and engine packages, ZeroAlloc Results instead of exceptions, a clean ZeroAlloc analyzer run, and a first public release with CI, codecov and a docs site. This happens first because nothing is published yet, so the rename and package split cost a find-and-replace now and a breaking change later. See `docs/superpowers/specs/2026-10-30-roadmap-design.md`.

## Definition of Done

- [x] All planned phases complete
- [x] All tests passing
- [x] No `ArchGov` names remain in code, packages, CLI or docs, apart from historical plan documents
- [x] `Ariadne.NET.Abstractions` and `Ariadne.NET` build as separate packages
- [x] Public engine APIs return ZeroAlloc Results instead of throwing for expected failures
- [x] GitHub repo exists with CI, codecov and docs site, and a first version is released by release-please

## Phases

1. Phase 1.1 — Rename to Ariadne.NET [complete]
2. Phase 1.2 — Package split [complete]
3. Phase 1.3 — Family conventions [complete]
4. Phase 1.4 — Publish [complete]

## Audit History

| Date | Verdict | Gaps |
|---|---|---|
| 2026-11-04 | PASS | No pre-push-review report on file; code quality was reviewed per task and per branch in the subagent-driven runs. See `docs/superpowers/reviews/2026-11-04-milestone-1-audit.md`. |
