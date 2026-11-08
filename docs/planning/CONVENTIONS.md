# Project Conventions

> Written by `init-conventions`. Do not hand-edit — re-run the sub-skill instead; the Commit & Release Protocol reads these fields.

**Established:** 2026-10-30

## Stack

**Language / runtime:** C#, .NET 10
**Package manager:** NuGet (dotnet CLI)
**Framework:** ASP.NET Core, System.CommandLine, ModelContextProtocol SDK
**Datastore:** n/a
**Build:** `dotnet build src/Ariadne.NET.slnx`
**Test:** `dotnet test src/Ariadne.NET.slnx`

## Commits

**Format:** conventional
**Scopes:** free
**Scope source:** n/a
**Fallback when scope not allowed:** omit scope

## Branching

**Model:** feature-branch
**PR required:** yes (confirmed by user; no remote to detect from)
**Protected branches:** main

## Versioning & Release

**Scheme:** semver (no tags yet; release-please produces vX.Y.Z)
**Released by:** release-please
**Milestone completion tags a release:** no
**Changelog:** auto

## Deployment

**Deploy target:** nuget.org for Ariadne.NET and Ariadne.NET.Abstractions, GitHub Pages for docs
**Environments:** github-pages
**Deployed by:** GitHub Actions
