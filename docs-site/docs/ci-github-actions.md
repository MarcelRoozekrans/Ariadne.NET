---
sidebar_position: 4
title: CI on GitHub Actions
---

# CI on GitHub Actions

Two workflows fail a GitHub Actions build when a solution breaks its architecture rules. One runs the `ariadne` tool and sends the result to code scanning. The other runs an architecture test with `dotnet test`. Both are in [`examples/github-actions/`](https://github.com/MarcelRoozekrans/Ariadne.NET/tree/main/examples/github-actions). Copy one to `.github/workflows/` and replace `MySolution.slnx` with your own `.sln`, `.slnx`, `.slnf` or `.csproj`.

This repository's own CI runs the same `ariadne validate` command line against a broken and a clean fixture solution. The job needs exit code 1 and a SARIF result for the broken one, and exit code 0 for the clean one.

## Exit codes

| Exit code | Meaning |
|-----------|---------|
| 0 | No violations. |
| 1 | The solution has violations. |
| 2 | Ariadne could not run, for example because no rule pack was found or the solution did not load. |

A usage error, such as an unknown `--format` value, currently exits 1 as well, so a script cannot tell it from a run with violations. See [issue #70](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/70). Any non-zero exit code fails a workflow step.

## Restore first

Ariadne never restores. The solution must compile, and the banned-packages rules read `obj/project.assets.json`, which only a restore creates. The tool workflow therefore runs `dotnet restore` before the check. The test workflow has no restore step, because `dotnet test` restores on its own.

## Fail the build with the tool

This workflow installs the `Ariadne.NET.Cli` dotnet tool (see [Install the tool](../getting-started.md#install-the-tool)), validates the solution and uploads the SARIF report.

```yaml
# Fails the build when the solution breaks its architecture rules, using the
# Ariadne.NET.Cli dotnet tool, and shows each violation in code scanning.
#
# Copy this file to .github/workflows/ and replace MySolution.slnx below with
# your own .sln, .slnx, .slnf or .csproj.
name: Architecture

on: [push, pull_request]

permissions:
  contents: read
  # Needed by the upload step.
  security-events: write

jobs:
  ariadne:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7

      - name: Setup .NET
        uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'

      # Restore first: the banned-packages rules read obj/project.assets.json.
      - name: Restore
        run: dotnet restore MySolution.slnx

      - name: Install Ariadne
        run: dotnet tool install --global Ariadne.NET.Cli

      # This step fails the job: exit code 1 on violations, 2 when Ariadne
      # cannot run at all.
      - name: Validate the architecture
        run: ariadne validate MySolution.slnx --format sarif --output ariadne.sarif

      # Runs after a failed validation too, so the violations reach code
      # scanning. Code scanning must be available for the repository: private
      # repositories need GitHub Advanced Security.
      - name: Upload the SARIF report
        if: always()
        uses: github/codeql-action/upload-sarif@v4
        with:
          sarif_file: ariadne.sarif
```

The validate step is the one that fails the job. The upload step has `if: always()`, so it runs after a failed validation and the violations still reach code scanning. The step needs the `security-events: write` permission.

### Code scanning

Uploaded results appear as code scanning alerts. Two limits apply:

- **Private repositories need GitHub Advanced Security.** Without it the upload step fails. Remove the upload step and the `security-events: write` permission; the validate step still fails the build.
- **Violations without a file location are not displayed.** A rule or layer that matches no type (`VacuousRule`, `VacuousLayer`) and a banned package have no source position. They are in the SARIF file and in the text report, and GitHub code scanning does not show them. They still make `ariadne validate` exit 1. See [issue #71](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/71).

The SARIF mapping is described in [Reports](how-it-works/14-reports.md#sarif-report).

## Fail the build with a test

This workflow only runs `dotnet test`. The architecture check is a test in your test project.

```yaml
# Fails the build when the solution breaks its architecture rules, as a test.
#
# The test project references the Ariadne.NET.Testing package and calls
# ArchitectureCheck.AssertValid() in a test. A violation is a failing test, so
# dotnet test fails the job.
#
# Copy this file to .github/workflows/ and replace MySolution.slnx below with
# your own solution.
name: Architecture tests

on: [push, pull_request]

permissions:
  contents: read

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7

      - name: Setup .NET
        uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'

      - name: Test
        run: dotnet test MySolution.slnx
```

Add the `Ariadne.NET.Testing` package to the test project and call `ArchitectureCheck.AssertValid()`:

```bash
dotnet add package Ariadne.NET.Testing
```

```csharp
// xUnit
public class ArchitectureTests
{
    [Fact]
    public void Solution_follows_its_architecture_rules() => ArchitectureCheck.AssertValid();
}
```

```csharp
// NUnit
[TestFixture]
public class ArchitectureTests
{
    [Test]
    public void Solution_follows_its_architecture_rules() => ArchitectureCheck.AssertValid();
}
```

```csharp
// MSTest
[TestClass]
public class ArchitectureTests
{
    [TestMethod]
    public void Solution_follows_its_architecture_rules() => ArchitectureCheck.AssertValid();
}
```

A violation is a failing test, and the exception message is the text `ariadne validate` prints. The test finds the solution from the test's output folder and the packs from the nearest `.architecture` folder. The package, its requirements and the failure types are described in [Testing](how-it-works/13-testing.md).

A test can also write a SARIF or JSON report before it throws, for a later step to upload. See [Reports in Testing](how-it-works/13-testing.md#reports).

## Which one to use

The tool workflow gives code scanning alerts and needs no change to your solution. The test workflow needs no extra tool and runs wherever your tests run, including locally. Both check the same packs with the same engine, so they agree. Use both if you want alerts and a local failing test.
