using Ariadne.NET.Analysis;
using Ariadne.NET.Reports;
using FluentAssertions;
using Xunit;

namespace Ariadne.NET.Tests.Reports;

public class TextReportWriterTests
{
    private static ArchitectureReport Report(params ArchitectureViolation[] violations) => new()
    {
        Violations = violations,
        Projects = [new CheckedProject("App", ["net10.0"])],
        SkippedProjects = [new SkippedProject("Legacy", "not supported")],
        TypesChecked = 7,
    };

    private static string Render(ArchitectureReport report, string root)
    {
        using var writer = new StringWriter();
        TextReportWriter.Write(writer, report, root);
        return writer.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ValidReport_PrintsPass()
    {
        var text = Render(Report(), "/repo");

        text.Should().Be("Checked 7 types in 1 projects\nSkipped Legacy: not supported\nResult: PASS - No violations found.\n");
    }

    [Fact]
    public void Write_ViolationWithPack_ShowsPackAfterFrameworks()
    {
        var root = Path.GetFullPath("/repo");
        var violation = new ArchitectureViolation
        {
            RuleId = "R1",
            Kind = ViolationKind.DependencyNotAllowed,
            SourceType = "A",
            Message = "A must not depend on B.",
            Pack = "shop-rules",
            Location = new SourceLocation(Path.Combine(root, "src", "A.cs"), 12, 3),
            TargetFrameworks = ["net10.0"],
        };

        var text = Render(Report(violation), root);

        text.Should().Be(
            "Checked 7 types in 1 projects\nSkipped Legacy: not supported\n" +
            "Result: FAIL - 1 violation(s) found.\n\n" +
            "  [DependencyNotAllowed] src/A.cs:12 (net10.0) pack shop-rules\n    A must not depend on B.\n");
    }

    [Fact]
    public void Write_ViolationWithoutPackOrLocation_OmitsPack()
    {
        var violation = new ArchitectureViolation
        {
            RuleId = "R1",
            Kind = ViolationKind.DependencyNotAllowed,
            SourceType = "Ns.A",
            Message = "msg",
        };

        var text = Render(Report(violation), "/repo");

        text.Should().Contain("  [DependencyNotAllowed] Ns.A\n    msg\n").And.NotContain(" pack ");
    }

    [Fact]
    public void Write_ViolationWithBecause_ShowsRuleIdAndBecauseUnderTheMessage()
    {
        var violation = new ArchitectureViolation
        {
            RuleId = "SHOP-NO-INFRA",
            Kind = ViolationKind.ForbiddenDependency,
            SourceType = "Ns.A",
            Message = "msg",
            Pack = "shop-rules",
            Because = "Storage can change without touching the domain.",
        };

        var text = Render(Report(violation), "/repo");

        text.Should().EndWith(
            "  [ForbiddenDependency] Ns.A pack shop-rules\n    msg\n    Rule SHOP-NO-INFRA: Storage can change without touching the domain.\n");
    }
}
