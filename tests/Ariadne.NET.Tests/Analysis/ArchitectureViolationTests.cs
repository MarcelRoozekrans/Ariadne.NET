using Ariadne.NET.Analysis;
using FluentAssertions;

namespace Ariadne.NET.Tests.Analysis;

public class ArchitectureViolationTests
{
    private static ArchitectureReport ReportWith(params ArchitectureViolation[] violations) => new()
    {
        Violations = violations,
        Projects = [new CheckedProject("App", ["net10.0"])],
        TypesChecked = 3,
    };

    [Fact]
    public void Report_WithNoViolations_IsValid() =>
        ReportWith().IsValid.Should().BeTrue();

    [Fact]
    public void Report_WithOneViolation_IsNotValid()
    {
        var violation = new ArchitectureViolation
        {
            RuleId = "R1",
            Kind = ViolationKind.DependencyNotAllowed,
            SourceType = "App.A",
            Message = "A must not depend on B",
        };

        ReportWith(violation).IsValid.Should().BeFalse();
    }

    [Fact]
    public void TestCompilation_WithBrokenSource_Throws()
    {
        var act = () => TestCompilation.Create("class {");

        act.Should().Throw<InvalidOperationException>();
    }
}
