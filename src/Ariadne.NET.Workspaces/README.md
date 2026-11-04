# Ariadne.NET.Workspaces

Loads .NET solutions and projects with `MSBuildWorkspace`, so Ariadne.NET can check architecture rules against the compiler's semantic model.

Every project that references this package needs its own reference that keeps the MSBuild assemblies out of its output:

```xml
<PackageReference Include="Microsoft.Build.Framework" Version="17.11.48" ExcludeAssets="runtime" />
```

The full documentation follows with the validator.
