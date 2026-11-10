using System.Text.Json;
using System.Text.Json.Nodes;
using Ariadne.NET.Analysis;
using Ariadne.NET.Reports;
using FluentAssertions;
using Xunit;

namespace Ariadne.NET.Tests.Reports;

public class SarifReportWriterTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ariadne-sarif-repo"));

    private static ArchitectureViolation Violation(string ruleId, string pack, string? file, int line = 1, int column = 1, string because = "Because.") => new()
    {
        RuleId = ruleId,
        Pack = pack,
        Kind = ViolationKind.ForbiddenDependency,
        SourceType = "Shop.Domain.Order",
        Message = $"{ruleId} broken.",
        Because = because,
        Location = file is null ? null : new SourceLocation(file, line, column),
        TargetFrameworks = ["net10.0"],
    };

    private static ArchitectureReport TwoPacks() => new()
    {
        Violations =
        [
            Violation("shared-rule", "clean-architecture", Path.Combine(Root, "src", "Shop.Domain", "Order.cs"), 12, 9, "The domain stays free of infrastructure."),
            Violation("shared-rule", "hexagonal", Path.Combine(Root, "src", "Shop Web", "Home.cs"), 3, 1),
            Violation("shared-rule", "clean-architecture", Path.Combine(Root, "src", "Shop.Domain", "Line.cs"), 4, 5),
            new ArchitectureViolation
            {
                RuleId = "ROLE-Handler",
                Pack = "cqrs",
                Kind = ViolationKind.VacuousRule,
                SourceType = "ROLE-Handler",
                Message = "Role Handler selects no type.",
            },
        ],
        Projects = [new CheckedProject("Shop.Domain", ["net10.0"])],
        TypesChecked = 7,
    };

    private static ArchitectureReport Empty() => new()
    {
        Violations = [],
        Projects = [new CheckedProject("App", ["net10.0"])],
        TypesChecked = 3,
    };

    private static string Render(ArchitectureReport report, string? root = null)
    {
        using var stream = new MemoryStream();
        SarifReportWriter.Write(stream, report, new ReportContext(root ?? Root, "1.2.3"));
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static JsonNode Golden(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Reports", "Golden", name))
            .Replace("{root}", new Uri(Root + Path.DirectorySeparatorChar).AbsoluteUri, StringComparison.Ordinal))!;

    [Fact]
    public void Write_TwoPacks_ValidatesAgainstSchema() => SarifSchema.Validate(Render(TwoPacks())).Should().BeEmpty();

    [Fact]
    public void Write_Empty_ValidatesAgainstSchema() => SarifSchema.Validate(Render(Empty())).Should().BeEmpty();

    [Fact]
    public void Write_ViolationWithoutLocation_ValidatesAndUsesLogicalLocation()
    {
        var json = Render(TwoPacks());
        SarifSchema.Validate(json).Should().BeEmpty();
        using var doc = JsonDocument.Parse(json);

        var location = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[3].GetProperty("locations")[0];

        location.TryGetProperty("physicalLocation", out _).Should().BeFalse();
        location.GetProperty("logicalLocations")[0].GetProperty("name").GetString().Should().Be("ROLE-Handler");
    }

    [Fact]
    public void Write_TwoPacks_MatchesGolden()
    {
        var actual = JsonNode.Parse(Render(TwoPacks()))!;

        JsonNode.DeepEquals(actual, Golden("report-twopacks.sarif")).Should().BeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Write_Empty_MatchesGolden()
    {
        var actual = JsonNode.Parse(Render(Empty()))!;

        JsonNode.DeepEquals(actual, Golden("report-empty.sarif")).Should().BeTrue(actual.ToJsonString());
    }

    [Fact]
    public void Write_SameRuleIdInTwoPacks_GivesTwoDescriptorsAndMatchingRuleIndex()
    {
        using var doc = JsonDocument.Parse(Render(TwoPacks()));
        var run = doc.RootElement.GetProperty("runs")[0];
        var rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules");

        rules.GetArrayLength().Should().Be(3);
        rules[0].GetProperty("id").GetString().Should().Be("shared-rule");
        rules[1].GetProperty("id").GetString().Should().Be("shared-rule");
        rules[0].GetProperty("properties").GetProperty("pack").GetString().Should().Be("clean-architecture");
        rules[1].GetProperty("properties").GetProperty("pack").GetString().Should().Be("hexagonal");
        rules[0].GetProperty("help").GetProperty("text").GetString().Should().Be("The domain stays free of infrastructure.");
        rules[0].GetProperty("shortDescription").GetProperty("text").GetString().Should().Be("ForbiddenDependency");
        var indexes = run.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("ruleIndex").GetInt32());
        indexes.Should().Equal(0, 1, 0, 2);
    }

    [Fact]
    public void Write_Result_HasErrorLevelRelativeUriAndSrcRootBase()
    {
        using var doc = JsonDocument.Parse(Render(TwoPacks()));
        var run = doc.RootElement.GetProperty("runs")[0];
        var result = run.GetProperty("results")[0];
        var artifact = result.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation");

        result.GetProperty("level").GetString().Should().Be("error");
        artifact.GetProperty("uri").GetString().Should().Be("src/Shop.Domain/Order.cs");
        artifact.GetProperty("uriBaseId").GetString().Should().Be("%SRCROOT%");
        run.GetProperty("results")[1].GetProperty("locations")[0].GetProperty("physicalLocation")
            .GetProperty("artifactLocation").GetProperty("uri").GetString().Should().Be("src/Shop%20Web/Home.cs");
        var region = result.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("region");
        region.GetProperty("startLine").GetInt32().Should().Be(12);
        region.GetProperty("startColumn").GetInt32().Should().Be(9);
        result.GetProperty("properties").GetProperty("pack").GetString().Should().Be("clean-architecture");
        result.GetProperty("properties").GetProperty("targetFrameworks")[0].GetString().Should().Be("net10.0");
    }

    [Fact]
    public void Write_Run_HasOriginalUriBaseIdsEndingInSlash()
    {
        using var doc = JsonDocument.Parse(Render(Empty()));

        var uri = doc.RootElement.GetProperty("runs")[0].GetProperty("originalUriBaseIds").GetProperty("%SRCROOT%").GetProperty("uri").GetString()!;

        uri.Should().StartWith("file:///").And.EndWith("/ariadne-sarif-repo/");
        doc.RootElement.GetProperty("version").GetString().Should().Be("2.1.0");
        doc.RootElement.GetProperty("$schema").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Write_PathOutsideRoot_UsesAbsoluteFileUriWithoutBaseId()
    {
        var outside = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "elsewhere", "X.cs"));
        var report = new ArchitectureReport { Violations = [Violation("r", "p", outside)], Projects = [], TypesChecked = 1 };
        var json = Render(report);
        SarifSchema.Validate(json).Should().BeEmpty();
        using var doc = JsonDocument.Parse(json);

        var artifact = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0].GetProperty("locations")[0]
            .GetProperty("physicalLocation").GetProperty("artifactLocation");

        artifact.GetProperty("uri").GetString().Should().Be(new Uri(outside).AbsoluteUri);
        artifact.TryGetProperty("uriBaseId", out _).Should().BeFalse();
    }

    [Fact]
    public void Write_MissingBecause_GivesEmptyHelpText()
    {
        var report = new ArchitectureReport { Violations = [Violation("r", "p", null, because: "")], Projects = [], TypesChecked = 1 };
        var json = Render(report);
        SarifSchema.Validate(json).Should().BeEmpty();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("runs")[0].GetProperty("tool").GetProperty("driver").GetProperty("rules")[0]
            .GetProperty("help").GetProperty("text").GetString().Should().BeEmpty();
    }

    [Fact]
    public void Write_Output_HasNoByteOrderMark_AndNamesTool()
    {
        var json = Render(Empty());
        json[0].Should().Be('{');
        using var doc = JsonDocument.Parse(json);
        var driver = doc.RootElement.GetProperty("runs")[0].GetProperty("tool").GetProperty("driver");
        driver.GetProperty("name").GetString().Should().Be("Ariadne.NET");
        driver.GetProperty("version").GetString().Should().Be("1.2.3");
        driver.GetProperty("informationUri").GetString().Should().Be("https://marcelroozekrans.github.io/Ariadne.NET/");
    }

    [Fact]
    public void Write_NullArguments_Throw()
    {
        var context = new ReportContext(Root, "1");
        FluentActions.Invoking(() => SarifReportWriter.Write(null!, Empty(), context)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => SarifReportWriter.Write(new MemoryStream(), null!, context)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => SarifReportWriter.Write(new MemoryStream(), Empty(), null!)).Should().Throw<ArgumentNullException>();
    }
}
