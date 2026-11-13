# Ariadne.NET.Workspaces

Loads .NET solutions and projects with `MSBuildWorkspace`, so Ariadne.NET can check architecture rules against the compiler's semantic model. It brings in `Ariadne.NET` and `Ariadne.NET.Abstractions`.

## Requirements

- **A .NET SDK on the machine.** The package finds it with MSBuildLocator and loads MSBuild from it. With no SDK, the load fails with an `Unavailable` error.
- **A `Microsoft.Build.Framework` reference in every project that references this package.** It keeps the MSBuild assemblies out of your output, and the exclusion does not flow from the package to you:

  ```xml
  <PackageReference Include="Microsoft.Build.Framework" Version="17.14.28" ExcludeAssets="runtime" />
  ```

  Without it the build fails with `MSBL001`. The `BuildHost-netcore` and `BuildHost-net472` folders that the workspace needs come with the package and land in your output.
- **A restored solution.** Ariadne never restores. A project whose compilation is complete passes even if it was never restored, for example a project with no packages that targets the SDK's own framework. Any other project that does not compile fails the load, and when no `project.assets.json` is found the error adds `run 'dotnet restore' first`.

## Use

```csharp
using Ariadne.NET.Workspaces;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddAriadneNETServices()      // from Ariadne.NET
    .AddAriadneNETWorkspaces()    // ISolutionLoader and ISolutionValidator
    .BuildServiceProvider();

var validator = services.GetRequiredService<ISolutionValidator>();
var result = await validator.ValidateAsync(resolvedPack, "MyApp.slnx");
```

`ValidateAsync` returns `Result<ArchitectureReport, AriadneError>`. It loads the path, checks the projects, lists the skipped ones in the report and disposes the workspace. `ISolutionLoader.LoadAsync` returns a `LoadedSolution` with the compilations, one per project and target framework, when you want to pass them to the engine's `IArchitectureValidator` yourself. Dispose it when you are done, because it owns the workspace.

## Which projects are checked

The path may be a `.sln`, `.slnx`, `.slnf` or `.csproj` file, or a directory that holds exactly one of them. The projects the path names are checked, test projects included: every project of a `.sln` or `.slnx`, the listed projects of a `.slnf`, the one project of a `.csproj`. A project that one of them references is compiled as a reference only.

Projects that are not C#, such as `.fsproj`, `.vbproj`, `.dcproj` and `.shproj`, are skipped and listed in the report's `SkippedProjects`. A project that targets several frameworks is checked once per target framework.

## When a load fails

A load is complete or it is an error. It fails on a compile error (the error lists the first 5), on a `System.Object` that does not resolve, on a source file that a project lists and that is missing on disk, and on a project that is not in the workspace. MSBuildWorkspace reports MSBuild warnings as failures too, so a workspace failure fails the load only when it names no project, names a project the workspace does not hold, names a project left without references or documents, or names a project that does not compile. In the last case the error is the compile error, with the workspace failures after it; in the others it is the workspace failures. A project with workspace failures that compiles is checked. Warnings do not fail it.

See the [documentation site](https://marcelroozekrans.github.io/Ariadne.NET/) for what counts as a dependency and for the rules the validator applies.
