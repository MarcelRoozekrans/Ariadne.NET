# Session State

**Last session:** 2026-11-03
**Repository:** `MarcelRoozekrans/Ariadne.NET`, public since 2026-11-03. Milestone 1 merged to `main` through PR #5 as merge commit `ef725d0`. The old private repository `MarcelRoozekrans/architecture-governance-engine` is frozen and stays private.

## Current Position

- **Milestone:** 1 — Ariadne.NET identity and family shape [active]
- **Phase:** 1.4 — Publish [tasks 1–12 done; task 13, the 0.1.0 release, waits on the owner's settings checklist]
- **Plan:** `docs/superpowers/plans/2026-11-03-phase-1.4-publish.md`; spec `docs/superpowers/specs/2026-10-31-phase-1.4-publish-design.md`

Phase 1.4 so far:
- Tasks 1–9 ran subagent-driven with a task review each, then a whole-branch review, one fix wave and a clean re-review.
- The `Getting started` sample loads the built-in clean-architecture pack and checks a directory with `ISourceValidator`. `pack-validate` runs it against the packed packages twice: clean code must exit 0, a Domain file using a WebApi namespace must exit 1.
- The first PR run failed only on Windows: `SyncCommand` joined `.architecture/imports.yaml` with `Path.Combine`, giving mixed separators. Fixed in the CLI, plus one test that expected the mixed path.
- On `main` after the merge: CI, baseline-checks and Deploy Docs green; the docs site answers at https://marcelroozekrans.github.io/Ariadne.NET/. Release Please fails at the token step until the release bot App settings exist.
- `Main` ruleset active, requiring `build (ubuntu-latest)`, `build (windows-latest)`, `aot-smoke`, `pack-validate`, `baseline / gitleaks`, `baseline / codeql (csharp)`, `baseline / nuget-audit` and `codecov/project`.

## Decided 2026-11-03: a fresh repository instead of a rename

The local history scan, gitleaks 8.30.1 with default rules plus private-detail rules, found no secrets, but the owner's home-domain email on 318 author and committer entries and a home-directory path in three old commits of this file. In-place rewriting could not remove them: GitHub keeps `refs/pull/*` for the old repository's 50 bot PRs, and only GitHub Support can delete those. The owner chose a fresh repository:
- History of `main` and `fix/mcp-composite-template` rewritten with git-filter-repo 2.47.0: a mailmap maps the home email to the GitHub noreply address, and the home paths are replaced. Trees are unchanged. gitleaks on the rewrite: no leaks.
- `MarcelRoozekrans/Ariadne.NET` created private with Actions off, the two branches pushed, then made public with CodeQL default setup off, Pages from Actions and Actions on.
- Local clones: the remote `ariadne` points at the new repository; `origin` still points at the old one, so branches with old history cannot be pushed into the new repository by accident. Push with `git push ariadne HEAD:<branch>`. The repository's git `user.email` is the GitHub noreply address.

## Open Decisions

- ZeroAlloc.Analyzers: settled at 1.12.0.
- An own YAML library: a separate, unscheduled project outside Ariadne (agreed 2026-10-31).
- From the roadmap design, none blocking: whether M3 ships a Roslyn analyzer; code generation's role after M4; Mnemosyne.NET integration.

## Blockers

- **Owner settings for the release**, from `RELEASING.md`: release bot App installed on `MarcelRoozekrans/Ariadne.NET`; variable `RELEASE_BOT_CLIENT_ID` and secret `RELEASE_BOT_PRIVATE_KEY`; variable `NUGET_USER`; Trusted Publishing policy allows new package ids; `Ariadne.NET` id prefix reserved; codecov already reports on the new repository.
- **actionlint false positives:** actionlint 1.7.12 reports two errors on `actions/create-github-app-token@v3` `client-id`, an actionlint metadata bug tracked as rhysd/actionlint#648 and #669. Expect exactly these two until it ships a fix.

## Follow-ups

- `MarcelRoozekrans/Ariadne.NET#1`: `SourceValidator` misses usings inside a file-scoped namespace and scans `bin/` and `obj/`.
- Open Renovate and Dependabot PRs on the new repository, from before the merge: review after the release.

## Recommended Next Step

Plan task 13. Confirm with the owner that the settings above are done, then re-run the failed Release Please run on `main` (`gh run rerun 41719092172 -R MarcelRoozekrans/Ariadne.NET`; the workflow has no dispatch trigger), or let the next push to `main` start it. Check that the release PR proposes 0.1.0 and that its changelog holds every Milestone 1 `feat:` and `fix:`; merge it with the owner's consent; watch the dispatched `ci.yml` publish on `v0.1.0`; verify both packages resolve on nuget.org and the release carries both `.nupkg`, both `.snupkg` and the `.cdx.json` SBOM. Then mark Phase 1.4 and Milestone 1 complete in `ROADMAP.md` and here.

Commit rules: conventional commits; no `Co-Authored-By` trailer or session link; no nested parentheses in commit bodies.
