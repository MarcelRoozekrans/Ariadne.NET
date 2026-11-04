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

    [Fact]
    public void TestCompilation_WithValidSource_HasNoErrors()
    {
        var compilation = TestCompilation.Create("class A { }");

        compilation.GetTypeByMetadataName("A").Should().NotBeNull();
        compilation.SyntaxTrees.Should().ContainSingle().Which.FilePath.Should().Be("Test.cs");
    }

    [Fact]
    public void TestCompilation_WithMultipleFiles_KeepsTheirPaths()
    {
        var compilation = TestCompilation.Create(("A.cs", "class A { B? b; }"), ("B.cs", "class B { }"));

        compilation.SyntaxTrees.Select(t => t.FilePath).Should().BeEquivalentTo("A.cs", "B.cs");
    }

    [Fact]
    public void Violation_Defaults_AreDocumented()
    {
        var violation = new ArchitectureViolation
        {
            RuleId = "R1",
            Kind = ViolationKind.MissingInterface,
            SourceType = "App.A",
            Message = "m",
        };

        violation.ReferenceCount.Should().Be(1);
        violation.Because.Should().BeEmpty();
        violation.TargetFrameworks.Should().BeEmpty();
        violation.Target.Should().BeNull();
        violation.Location.Should().BeNull();
    }

    [Fact]
    public void Report_Defaults_HaveNoSkippedProjects() =>
        ReportWith().SkippedProjects.Should().BeEmpty();

    [Fact]
    public void AnalyzedProject_Defaults_HaveNoGeneratedTrees()
    {
        var project = new AnalyzedProject("App", null, TestCompilation.Create("class A { }"));

        project.GeneratedTrees.Should().BeEmpty();
    }
}
