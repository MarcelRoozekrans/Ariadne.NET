using Ariadne.NET.Analysis;
using FluentAssertions;

namespace Ariadne.NET.Testing.Tests;

// Phase 3.2 (the JSON and SARIF report writers) builds on ArchitectureCheck.Run, so it is pinned here by name and
// signature. The other members phase 3.2 uses are pinned in Ariadne.NET.Workspaces.Tests/PackSetSurfaceTests.cs, which
// does not reference Ariadne.NET.Testing.
public sealed class ArchitectureCheckSurfaceTests
{
    [Fact]
    public void ArchitectureCheck_Run_takes_an_optional_path_and_returns_a_report()
    {
        Func<string?, ArchitectureReport> run = ArchitectureCheck.Run;

        run.Method.GetParameters().Should().ContainSingle();
    }
}
