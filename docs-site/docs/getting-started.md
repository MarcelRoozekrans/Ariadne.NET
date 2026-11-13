---
sidebar_position: 2
title: Getting started
---

# Getting started

Load a built-in rule pack and check a solution or project against it in a .NET console app.

Coming from ArchUnitNET? See the [migration guide](migrating-from-archunitnet.md).

```bash
dotnet new console -o MyApp --framework net10.0
cd MyApp
dotnet add package Ariadne.NET.Workspaces
dotnet add package Microsoft.Extensions.DependencyInjection
```

`Ariadne.NET.Workspaces` brings in `Ariadne.NET` and `Ariadne.NET.Abstractions`. Ariadne.NET references only the dependency injection abstractions, so the app needs the container package to build a service provider.

Add one more reference to `MyApp.csproj`. `dotnet add package` cannot set `ExcludeAssets`, so edit the file:

```xml
<PackageReference Include="Microsoft.Build.Framework" Version="17.14.28" ExcludeAssets="runtime" />
```

Ariadne.NET.Workspaces loads projects with MSBuild from the installed .NET SDK, and it needs this reference to keep the MSBuild assemblies out of your output. Without it the build fails with `MSBL001`.

Replace `Program.cs` with:

```csharp
using Ariadne.NET.Abstractions;
using Ariadne.NET.Workspaces;
using Microsoft.Extensions.DependencyInjection;

using var services = new ServiceCollection().AddAriadneNETServices().AddAriadneNETWorkspaces().BuildServiceProvider();
var loader = services.GetRequiredService<IRulePackLoader>();
var resolver = services.GetRequiredService<IRulePackResolver>();
var validator = services.GetRequiredService<ISolutionValidator>();

// A rule pack that builds on the built-in clean-architecture pack.
var pack = loader.LoadFromString("""
    schema: 2
    name: my-service
    extends:
      - builtin://clean-architecture
    """);
if (pack.IsFailure)
{
    Console.Error.WriteLine(pack.Error.Message);
    return 2;
}

var resolved = resolver.Resolve(pack.Value);
if (resolved.IsFailure)
{
    Console.Error.WriteLine(resolved.Error.Message);
    return 2;
}

Console.WriteLine($"{resolved.Value.Name}: {resolved.Value.Layers.Count} layers, {resolved.Value.ComponentRoles.Count} component roles");
foreach (var layer in resolved.Value.Layers)
{
    Console.WriteLine($"  {layer.Name} -> [{string.Join(", ", layer.AllowedDependencies)}]");
}

// Check a solution or project against the pack: the first argument, else the current directory.
var result = await validator.ValidateAsync(resolved.Value, args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
if (result.IsFailure)
{
    Console.Error.WriteLine(result.Error.Message);
    return 2;
}

var report = result.Value;
Console.WriteLine($"Checked {report.TypesChecked} types: {report.Violations.Count} violations");
foreach (var violation in report.Violations)
{
    var where = violation.Location is { } location ? $"{location.FilePath}:{location.Line}" : violation.SourceType;
    Console.WriteLine($"  [{violation.Kind}] {where}: {violation.Message}");
}

return report.IsValid ? 0 : 1;
```

`dotnet run` prints the resolved pack name, its layer and component role counts, and each layer with the layers it may depend on. The built-in layers cover namespaces such as `**.Domain.**`, so the check finds layers in a solution whose namespaces contain those segments; see [Rule Schema](how-it-works/03-rule-schema.md) to point a layer at your own namespaces or assemblies. It then checks the solution or project you pass as the first argument, or the one in the current directory, and prints how many types it checked. Each violation follows as `[Kind] file:line: message`. The exit code is 1 when there are violations and 2 when the pack or the solution cannot be loaded.

The path may be a `.sln`, `.slnx`, `.slnf` or `.csproj` file, or a directory that holds exactly one of them. Ariadne.NET.Workspaces needs a .NET SDK on the machine, and it never restores: run `dotnet restore` on the solution before you check it. To run the check from a unit test instead, see [Testing](how-it-works/13-testing.md). See [Validation](how-it-works/08-validation.md) for what counts as a dependency and which projects are checked.

## Install the tool

To check a solution without writing code, install the `ariadne` command as a .NET tool:

```bash
dotnet tool install --global Ariadne.NET.Cli
ariadne --version
```

`ariadne --version` prints the installed version. The tool needs the .NET 10 SDK on the machine, because it locates MSBuild at run time to load your solution. Update it with `dotnet tool update --global Ariadne.NET.Cli`.

Run your first check from the folder of a restored solution:

```bash
dotnet restore
ariadne init --template clean-architecture
ariadne validate
```

`ariadne init` writes `.architecture/rules.yaml`. `ariadne validate` loads the solution in the current folder, checks it against every pack in the `.architecture` folder, and exits 0 when it finds no violations, 1 when it finds violations and 2 when it cannot run. A usage error, such as an unknown `--format`, currently exits 1 as well (see [issue #70](https://github.com/MarcelRoozekrans/Ariadne.NET/issues/70)). Ariadne never restores, so run `dotnet restore` first. See [Host Applications](how-it-works/10-host-applications.md) for every command, and for the agent hooks that call `ariadne` from Claude Code.
