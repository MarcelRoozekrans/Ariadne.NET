# Ariadne.NET.Testing

Checks a .NET solution against its Ariadne.NET architecture rule packs from a unit test, in any test framework, with one call.

## Install

```bash
dotnet add package Ariadne.NET.Testing
```

The package brings in `Ariadne.NET.Workspaces`, `Ariadne.NET` and `Ariadne.NET.Abstractions`. It targets net10.0 and works with any test framework, because it only throws an exception.

## Requirements

- **A .NET SDK on the machine.** The check loads MSBuild from it with MSBuildLocator.
- **No extra reference.** The package ships a `buildTransitive` targets file that keeps `Microsoft.Build.Framework` out of your test project's output, so you add no `Microsoft.Build.Framework` line and the build does not fail with `MSBL001`. The `BuildHost-netcore` and `BuildHost-net472` folders the workspace needs land in your output.
- **A restored solution.** Ariadne never restores; run `dotnet restore` first. A project that does not compile fails the load.
- **A `.architecture` folder with at least one pack**, in the solution's folder or any folder above it. `ariadne init` creates one.

## Use

One call in a test. The solution is the single `.sln` or `.slnx` in the nearest folder at or above the test's output folder that holds one.

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

`AssertValid` and `AssertValidAsync` take an optional `solutionPath`: a `.sln`, `.slnx`, `.slnf` or `.csproj` file, or a folder that holds exactly one of them. `Run` and `RunAsync` return the `ArchitectureReport` instead of throwing on violations, for a test that wants to assert on it:

```csharp
var report = ArchitectureCheck.Run();
report.Violations.Should().BeEmpty();
```

A relative `solutionPath` starts at the test runner's current directory, usually `bin/<configuration>/<framework>` of the test project, not the source file's folder. Without a path the search for a solution starts at `AppContext.BaseDirectory`, the same folder, and walks up. To name a solution explicitly, build the path from that folder. For a test project at `repo/tests/MyApp.Tests`, the output folder `bin/Debug/net10.0` is five levels below the repo root; the number of `..` segments depends on where the test project sits:

```csharp
var solution = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MyApp.slnx"));
var report = ArchitectureCheck.Run(solution);
```

The synchronous methods block on the async check, so they also work in test frameworks without async support.

## Which packs are checked

Every `.yaml` or `.yml` file directly in the `.architecture` folder is a pack, except `imports.yaml` and `imports.yml`, the manifest `ariadne sync` reads, and each one is checked on its own. The folder is the nearest one found by walking up from the solution's folder. Files are checked in file name order, and subfolders such as `.architecture/.cache` are not scanned. The violations of all packs come back in one report, and each violation names its pack.

`ariadne validate`, the Claude Code stop hook and the MCP `validate_source` tool (when no `template` is given) check the same set, so a test and the command line agree.

## Limit: imported packs are not files in the folder

A pack that another pack imports must be a `builtin://` or `import://` reference. `extends` cannot point at a file in `.architecture`, and every file in the folder is checked as a pack in its own right, so a base pack saved there would be checked as if it were a complete pack. Put a shared base in a git repository and bring it in with `import://` and `ariadne sync`.

## Reports

A test can leave a JSON or SARIF report behind, for CI to upload or archive. The formats are described in [Reports](https://marcelroozekrans.github.io/Ariadne.NET/how-it-works/reports).

```csharp
ArchitectureCheck.AssertValid(null, "artifacts/architecture.sarif", ReportFormat.Sarif);
```

`ReportFormat` is in `Ariadne.NET.Reports`. The report file is written before the exception is thrown, so a failing test still leaves its report. The file's folder is created when missing and an existing file is overwritten. `AssertValidAsync` has the same overload with a `CancellationToken`.

To write the report of a `Run` call, use `WriteReport`:

```csharp
var report = ArchitectureCheck.Run();
ArchitectureCheck.WriteReport(report, ReportFormat.Json, "artifacts/architecture.json");
```

File paths in the report are relative to the repository root, the nearest ancestor with a `.git` entry. `AssertValid` uses the root of the solution it checked. A report does not record where its solution was, so `WriteReport` starts from `AppContext.BaseDirectory`, the test project's output folder. Both fall back to the starting folder when no ancestor has a `.git` entry.



The call throws one of two exceptions, and both derive from `ArchitectureCheckException`:

- `ArchitectureViolationException` is thrown by `AssertValid` and `AssertValidAsync` when the solution breaks a rule. Its message is the text `ariadne validate` prints: the number of types and projects checked, then each violation with its location and pack, its message and, when the rule has a `because`, a `Rule <id>: <because>` line. Its `Report` property holds the `ArchitectureReport`.
- `ArchitectureCheckException` is thrown by every method when the check could not run: no `.architecture` folder or no pack in it, an invalid pack (the message starts with the file name), two packs with the same name, no solution found, more than one solution in the nearest folder that has any, or a solution that failed to load. The message says why.

Neither exception exits the process. The test framework reports the failure like any other failed test.
