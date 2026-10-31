# Milestone 1: Ariadne.NET identity and family shape

**Status:** active
**Started:** 2026-10-30

## Goal

Rename the architecture governance engine to Ariadne.NET and give it the shape of its siblings in the Cretan family (Rag.NET, Minos.NET, Mnemosyne.NET): separate Abstractions and engine packages, ZeroAlloc Results instead of exceptions, a clean ZeroAlloc analyzer run, and a first public release with CI, codecov and a docs site. This happens first because nothing is published yet, so the rename and package split cost a find-and-replace now and a breaking change later. See `docs/superpowers/specs/2026-10-30-roadmap-design.md`.

## Definition of Done

- [ ] All planned phases complete
- [ ] All tests passing
- [ ] No `ArchGov` names remain in code, packages, CLI or docs, apart from historical plan documents
- [ ] `Ariadne.NET.Abstractions` and `Ariadne.NET` build as separate packages
- [ ] Public engine APIs return ZeroAlloc Results instead of throwing for expected failures
- [ ] GitHub repo exists with CI, codecov and docs site, and a first version is released by release-please

## Phases

1. Phase 1.1 — Rename to Ariadne.NET [complete]
2. Phase 1.2 — Package split [complete]
3. Phase 1.3 — Family conventions [active]
4. Phase 1.4 — Publish [pending]

## Audit History

| Date | Verdict | Gaps |
|---|---|---|
