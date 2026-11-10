# Ariadne.NET.Testing

Checks a .NET solution against its Ariadne.NET architecture rule packs from a unit test. It brings in `Ariadne.NET.Workspaces`, `Ariadne.NET` and `Ariadne.NET.Abstractions`.

The API is not written yet; this package is a work in progress.

## Requirements

- **A .NET SDK on the machine.** The check loads MSBuild from it through MSBuildLocator.
- **No extra reference.** Unlike `Ariadne.NET.Workspaces`, this package keeps the `Microsoft.Build.Framework` assembly out of your output itself, so your test project needs no `Microsoft.Build.Framework` reference and does not fail with `MSBL001`.
