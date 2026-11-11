using Ariadne.NET.Analysis;
using Ariadne.NET.Reports;
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

    [Fact]
    public void ArchitectureCheck_report_members_have_the_documented_signatures()
    {
        Action<ArchitectureReport, ReportFormat, string> write = ArchitectureCheck.WriteReport;
        Action<string?, string, ReportFormat> assertValid = ArchitectureCheck.AssertValid;
        Func<string?, string, ReportFormat, CancellationToken, Task> assertValidAsync = ArchitectureCheck.AssertValidAsync;

        write.Method.GetParameters().Should().HaveCount(3);
        assertValid.Method.GetParameters().Should().HaveCount(3);
        assertValidAsync.Method.GetParameters().Should().HaveCount(4);
    }
}
