# GitHub Actions examples

Two workflows that fail the build when a solution breaks its architecture rules. Copy one to `.github/workflows/` and replace `MySolution.slnx` with your own `.sln`, `.slnx`, `.slnf` or `.csproj`.

| File | How it checks | Line that fails the job |
|---|---|---|
| `ariadne-tool.yml` | Installs the `Ariadne.NET.Cli` dotnet tool, runs `ariadne validate` and uploads the SARIF report to code scanning. | `ariadne validate MySolution.slnx --format sarif --output ariadne.sarif`, which exits 1 on violations and 2 when it cannot run. |
| `ariadne-test.yml` | Runs your tests. A test project calls `ArchitectureCheck.AssertValid()` from the `Ariadne.NET.Testing` package. | `dotnet test MySolution.slnx`, which fails when the architecture test fails. |

## Adapting them

- Change the solution path in every step that names `MySolution.slnx`. Ariadne reads the rule packs from the nearest `.architecture` folder at or above the solution folder.
- In `ariadne-tool.yml`, keep `dotnet restore` before the validate step. The banned-packages rules read `obj/project.assets.json`, and Ariadne never restores.
- The upload step runs unless the job was cancelled and only when `ariadne.sarif` exists, so violations reach code scanning even though the validate step failed the job, and a run that wrote no report does not add an upload error. Code scanning must be available for the repository: private repositories need GitHub Advanced Security. Without it, drop the upload step and the `security-events: write` permission; the validate step still fails the build. The upload step is skipped when Dependabot pushes, because that run's token cannot write code scanning results. Pull request runs still upload, from forks and from Dependabot too. A private repository also needs the `actions: read` permission, which the example grants.
