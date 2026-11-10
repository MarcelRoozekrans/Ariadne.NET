# Ariadne.NET.Cli

The `ariadne` command line tool. It checks a .NET solution against the Ariadne.NET architecture rule packs in its `.architecture` folder, sets up those rules in a repository, and runs the Claude Code hooks that keep an agent inside them.

## Install

```bash
dotnet tool install --global Ariadne.NET.Cli
ariadne --version
```

The tool runs on .NET 10. It loads projects with MSBuild from the installed .NET SDK, so a .NET SDK must be on the machine, not only the runtime.

## Validate a solution

```bash
ariadne init --template clean-architecture
dotnet restore
ariadne validate
```

`ariadne init` writes `.architecture/rules.yaml`. `ariadne validate` takes a `.sln`, `.slnx`, `.slnf` or `.csproj`, or a folder that holds exactly one of them, and defaults to the current folder. It checks the solution against every pack in the nearest `.architecture` folder. Ariadne never restores, so run `dotnet restore` first.

`--format text|json|sarif` picks the report format, and `--output FILE` writes the report to a file instead of standard output:

```bash
ariadne validate src/MyApp.slnx --format sarif --output artifacts/architecture.sarif
```

## Exit codes

| Code | Meaning |
|---|---|
| 0 | The check ran and found no violations. |
| 1 | The check ran and found violations. |
| 2 | The command could not run: bad input, missing files, an invalid pack, a solution that failed to load. |

A CI step can fail on 1 and 2 alike, or tell a broken rule apart from a broken setup.

## Other commands

`ariadne rules` and `ariadne roles` show the resolved pack, `ariadne generate` places a new component, `ariadne sync` fetches imported packs, and `ariadne hook <session-start|pre-tool-use|stop>` runs a Claude Code hook. `ariadne init --agent claude` installs those hooks into `.claude/settings.json`. `ariadne --help` lists every command and option.

## Docs

The full documentation is at [marcelroozekrans.github.io/Ariadne.NET](https://marcelroozekrans.github.io/Ariadne.NET/).
