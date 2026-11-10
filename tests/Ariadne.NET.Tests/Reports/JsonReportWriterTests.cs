using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ariadne.NET.Analysis;
using Ariadne.NET.Reports;
using FluentAssertions;
using Xunit;

namespace Ariadne.NET.Tests.Reports;

public class JsonReportWriterTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ariadne-json-repo"));

    private static ArchitectureReport TwoPacks() => new()
    {
        Violations =
        [
            new ArchitectureViolation
            {
                RuleId = "no-domain-to-infra",
                Pack = "clean-architecture",
                Kind = ViolationKind.ForbiddenDependency,
                SourceType = "Shop.Domain.Order",
                Message = "Shop.Domain.Order must not depend on Shop.Infrastructure.Db.",
                Because = "The domain stays free of infrastructure.",
                Target = "Shop.Infrastructure.Db",
                Location = new SourceLocation(Path.Combine(Root, "src", "Shop.Domain", "Order.cs"), 12, 9),
                ReferenceCount = 2,
                TargetFrameworks = ["net10.0"],
            },
            new ArchitectureViolation
            {
                RuleId = "ROLE-Handler",
                Pack = "cqrs",
                Kind = ViolationKind.VacuousRule,
                SourceType = "ROLE-Handler",
                Message = "Role Handler selects no type.",
            },
        ],
        Projects = [new CheckedProject("Shop.Domain", ["net10.0"]), new CheckedProject("Shop.Web", ["net9.0", "net10.0"])],
        SkippedProjects = [new SkippedProject("Legacy", "not supported")],
        TypesChecked = 7,
    };

    private static ArchitectureReport Empty() => new()
    {
        Violations = [],
        Projects = [new CheckedProject("App", ["net10.0"])],
        TypesChecked = 3,
    };

    private static byte[] Render(ArchitectureReport report, string? root = null)
    {
        using var stream = new MemoryStream();
        JsonReportWriter.Write(stream, report, new ReportContext(root ?? Root, "1.2.3"));
        return stream.ToArray();
    }

    private static JsonNode Golden(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Reports", "Golden", name)))!;

    [Fact]
    public void Write_TwoPacks_MatchesGolden()
    {
        var actual = JsonNode.Parse(Render(TwoPacks()))!;

        JsonNode.DeepEquals(actual, Golden("report-twopacks.json")).Should().BeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Write_EmptyReport_MatchesGolden()
    {
        var actual = JsonNode.Parse(Render(Empty()))!;

        JsonNode.DeepEquals(actual, Golden("report-empty.json")).Should().BeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Write_Report_HasSchemaOne()
    {
        using var doc = JsonDocument.Parse(Render(Empty()));

        doc.RootElement.GetProperty("schema").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("tool").GetProperty("name").GetString().Should().Be("ariadne");
    }

    [Fact]
    public void Write_Violation_KeepsPropertyOrder()
    {
        using var doc = JsonDocument.Parse(Render(TwoPacks()));

        var names = doc.RootElement.GetProperty("violations")[0].EnumerateObject().Select(p => p.Name);

        names.Should().Equal("ruleId", "pack", "kind", "sourceType", "message", "because", "target", "location", "referenceCount", "targetFrameworks");
        doc.RootElement.EnumerateObject().Select(p => p.Name).Should()
            .Equal("schema", "tool", "typesChecked", "projects", "skippedProjects", "violations");
    }

    [Fact]
    public void Write_ViolationWithoutLocationOrTarget_OmitsThem()
    {
        using var doc = JsonDocument.Parse(Render(TwoPacks()));

        var second = doc.RootElement.GetProperty("violations")[1];

        second.TryGetProperty("location", out _).Should().BeFalse();
        second.TryGetProperty("target", out _).Should().BeFalse();
    }

    [Fact]
    public void Write_Output_HasNoByteOrderMark()
    {
        Render(Empty())[0].Should().Be((byte)'{');
    }

    [Fact]
    public void Write_PathOutsideRoot_KeepsPathWithForwardSlashes()
    {
        var outside = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "elsewhere", "X.cs"));
        var report = new ArchitectureReport
        {
            Violations =
            [
                new ArchitectureViolation
                {
                    RuleId = "r",
                    Kind = ViolationKind.ForbiddenDependency,
                    SourceType = "T",
                    Message = "m",
                    Location = new SourceLocation(outside, 1, 1),
                },
            ],
            Projects = [],
            TypesChecked = 1,
        };

        using var doc = JsonDocument.Parse(Render(report));

        var path = doc.RootElement.GetProperty("violations")[0].GetProperty("location").GetProperty("path").GetString()!;
        path.Should().Be(outside.Replace('\\', '/'));
    }

    [Fact]
    public void Write_NullArguments_Throw()
    {
        var context = new ReportContext(Root, "1");
        FluentActions.Invoking(() => JsonReportWriter.Write(null!, Empty(), context)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => JsonReportWriter.Write(new MemoryStream(), null!, context)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => JsonReportWriter.Write(new MemoryStream(), Empty(), null!)).Should().Throw<ArgumentNullException>();
    }
}
