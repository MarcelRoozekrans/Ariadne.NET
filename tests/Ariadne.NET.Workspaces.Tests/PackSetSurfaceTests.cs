using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Reports;
using FluentAssertions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces.Tests;

// Phase 3.2 (the JSON and SARIF report writers) builds on these members, so each is pinned here by name and signature:
// a rename or a signature change fails this build. The fourth, ArchitectureCheck.Run, lives in
// Ariadne.NET.Testing.Tests/ArchitectureCheckSurfaceTests.cs because this project does not reference Ariadne.NET.Testing.
public sealed class PackSetSurfaceTests
{
    [Fact]
    public void ArchitectureViolation_Pack_can_be_set_and_is_kept_by_with()
    {
        var violation = new ArchitectureViolation
        {
            RuleId = "R-1",
            Kind = ViolationKind.DependencyNotAllowed,
            SourceType = "A",
            Message = "m",
        };

        var tagged = violation with { Pack = "p" };

        tagged.Pack.Should().Be("p");
        violation.Pack.Should().BeEmpty();
    }

    [Fact]
    public void IPackSetValidator_ValidateAsync_takes_a_path_and_a_token_and_returns_a_report_result()
    {
        Func<IPackSetValidator, Func<string, CancellationToken, Task<Result<ArchitectureReport, AriadneError>>>> bind =
            validator => validator.ValidateAsync;

        bind.Should().NotBeNull();
    }

    [Fact]
    public void TextReportWriter_Write_takes_a_writer_a_report_and_a_root()
    {
        Action<TextWriter, ArchitectureReport, string> write = TextReportWriter.Write;
        var report = new ArchitectureReport { Violations = [], Projects = [], TypesChecked = 0 };
        using var output = new StringWriter();

        write(output, report, ".");

        output.ToString().Should().Contain("PASS");
    }
}
