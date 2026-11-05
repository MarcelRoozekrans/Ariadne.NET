# Releasing Ariadne.NET

Releases are cut by release-please and published to nuget.org by `ci.yml`. Nothing is published by hand.

## How a release happens

1. Conventional commits on `main` make release-please keep a release PR open, named `chore(main): release X.Y.Z`.
2. Merging that PR tags `vX.Y.Z` and creates the GitHub release.
3. `release-please.yml` then dispatches `ci.yml` on the tag with `publish_to_nuget=true`.
4. On that run, the jobs `publish-nuget` needs, `build`, `aot-smoke` and `pack-validate`, run on the tagged commit. `publish-nuget` starts only after they pass.
5. `publish-nuget` refuses a prerelease, or a tag that is not the manifest's version. It then packs with `-p:AriadneRelease=true`, inspects the packages, pushes the three `.nupkg` files, `Ariadne.NET`, `Ariadne.NET.Abstractions` and `Ariadne.NET.Workspaces`, to nuget.org, and attaches them and their `.snupkg` files to the GitHub release. nuget.org takes each `.snupkg` with the same push as its `.nupkg`.
6. The same job then attaches a CycloneDX SBOM, a `.cdx.json` file, to the GitHub release.

Every push and pull request also runs `pack-validate`. It packs the same way, inspects the packages, and rehearses the push against a local feed. It then restores a consumer that references `Ariadne.NET.Workspaces` from that feed and runs it twice: a clean project must exit 0, and a fixture with a forbidden dependency must exit 1.

## Before the first release: settings on nuget.org and GitHub

- **Release bot App:** the release bot GitHub App is installed on `MarcelRoozekrans/Ariadne.NET`.
- **`RELEASE_BOT_CLIENT_ID` and `RELEASE_BOT_PRIVATE_KEY`:** in the GitHub repository, add the variable `RELEASE_BOT_CLIENT_ID` and the secret `RELEASE_BOT_PRIVATE_KEY` from the App. `release-please.yml` mints its token from them.
- **`NUGET_USER`:** add a variable named `NUGET_USER` holding the nuget.org user name that owns the policy.
- **Trusted Publishing:** on nuget.org, a Trusted Publishing policy exists for the repository `MarcelRoozekrans/Ariadne.NET` and the workflow file `ci.yml` (done 2026-10-31). It must be allowed to push new packages as well as new versions, since all three ids are new on nuget.org.
- **Reserve the id prefix:** reserve the `Ariadne.NET` id prefix on nuget.org. Anyone can take an id until it is published.
- **Codecov:** add the repository on codecov, so the coverage upload from `build` is accepted.
- **CodeQL:** keep the default setup off. CodeQL runs in `baseline-checks.yml` through the ci-baseline v1 reusable workflow, and with default setup on, GitHub refuses that workflow's SARIF upload.

Without the App installed and its client id and private key, `release-please.yml` cannot mint a token, so no release PR is opened and no tag dispatch happens. Without `NUGET_USER`, the login in `publish-nuget` fails. Without the Trusted Publishing policy, the login or push fails and nothing reaches nuget.org. Without the reservation, anyone can claim the ids first.

## Re-running a publish

A push that died partway can be finished by dispatching the same run again on the tag:

    gh workflow run ci.yml --ref vX.Y.Z -f publish_to_nuget=true -f resume=true

With `resume=true` the push uses `--skip-duplicate`, so it skips the packages that already arrived. Dispatching on `main` publishes nothing, because the job runs only on a `v*` tag.

A release run, without `resume`, does not skip anything: if a package is already on nuget.org at the release version, the run fails and names it. That version was then published by something other than this pipeline. Check what is on nuget.org before resuming; a published version can be unlisted but never replaced, so a wrong package means releasing the next patch version.

A re-run skips a package that already arrived, and may not re-send its symbol package. If a symbol package is missing on nuget.org after a publish, it is attached to the GitHub release and can be uploaded on nuget.org's web upload page.

## When nuget.org refuses a push

nuget.org's reason is on the `error:` line just above the job's own error. Two refusals return 409:

- **The version already exists.** It was published by something other than this run. Check what is on nuget.org; a published version can be unlisted but never replaced.
- **"The package ID is reserved."** An ID prefix reservation covers the ID, and the owner of the Trusted Publishing policy does not hold it. Reservations are not shown on nuget.org; ask account@nuget.org which owner holds the prefix, then make that owner the policy's package owner or have the reservation moved.

## The version

The version is the `"."` entry of `.release-please-manifest.json`. A build without `-p:AriadneRelease=true` is versioned `X.Y.Z-local`, and that version is compiled into the DLLs. So every release pack builds with the switch, and the inspection rejects a DLL that does not carry the release version.
