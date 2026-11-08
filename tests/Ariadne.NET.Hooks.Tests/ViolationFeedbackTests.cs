using Ariadne.NET.Analysis;
using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class ViolationFeedbackTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "feedback-root");

    private static ArchitectureViolation Violation(int i, string because = "Because reasons.", string? file = "src/A.cs", string message = "A depends on B.") =>
        new()
        {
            RuleId = $"RULE-{i}",
            Kind = ViolationKind.ForbiddenDependency,
            SourceType = $"Shop.Type{i}",
            Message = message,
            Because = because,
            Location = file is null ? null : new SourceLocation(Path.Combine(Root, file), 7 + i, 1),
        };

    private static ArchitectureReport Report(params ArchitectureViolation[] violations) =>
        new() { Violations = violations, Projects = [], TypesChecked = 1 };

    [Fact]
    public void Lists_the_rule_id_because_message_and_location()
    {
        var text = ViolationFeedback.Build(Report(Violation(0)), Root);

        text.Should().StartWith("Ariadne found 1 architecture violation(s) in the solution after your edits this turn:");
        text.Should().Contain("[ForbiddenDependency] src/A.cs:7").And.Contain("A depends on B.").And.Contain("Rule RULE-0: Because reasons.");
    }

    [Fact]
    public void Without_because_the_rule_line_is_left_out_but_a_positive_control_has_it()
    {
        ViolationFeedback.Build(Report(Violation(0, because: "")), Root).Should().NotContain("Rule RULE-0");
        ViolationFeedback.Build(Report(Violation(0, because: "x")), Root).Should().Contain("Rule RULE-0: x");
    }

    [Fact]
    public void Without_a_location_the_source_type_is_shown() =>
        ViolationFeedback.Build(Report(Violation(0, file: null)), Root).Should().Contain("[ForbiddenDependency] Shop.Type0");

    [Fact]
    public void Paths_are_relative_and_use_forward_slashes() =>
        ViolationFeedback.Build(Report(Violation(0, file: Path.Combine("src", "Deep", "A.cs"))), Root)
            .Should().Contain("src/Deep/A.cs:7").And.NotContain(Root);

    [Fact]
    public void More_than_twenty_violations_list_twenty_and_a_tail()
    {
        var text = ViolationFeedback.Build(Report(Enumerable.Range(0, 25).Select(i => Violation(i)).ToArray()), Root);

        text.Should().StartWith("Ariadne found 25 architecture violation(s)");
        text.Should().Contain("Rule RULE-19:").And.NotContain("Rule RULE-20:");
        text.TrimEnd().Should().EndWith("and 5 more. Run 'ariadne validate' for the full list.");
    }

    [Fact]
    public void Exactly_twenty_violations_have_no_tail() =>
        ViolationFeedback.Build(Report(Enumerable.Range(0, 20).Select(i => Violation(i)).ToArray()), Root).Should().NotContain("more.");

    [Fact]
    public void A_custom_limit_is_honoured() =>
        ViolationFeedback.Build(Report(Enumerable.Range(0, 3).Select(i => Violation(i)).ToArray()), Root, maxViolations: 1)
            .Should().Contain("Rule RULE-0:").And.NotContain("Rule RULE-1:").And.Contain("and 2 more.");

    [Fact]
    public void Huge_messages_keep_the_text_within_ten_thousand_characters_and_keep_the_tail()
    {
        var huge = new string('m', 50_000);
        var text = ViolationFeedback.Build(
            Report(Enumerable.Range(0, 25).Select(i => Violation(i, because: huge, message: huge)).ToArray()), Root);

        text.Length.Should().BeLessThanOrEqualTo(10_000);
        text.Should().StartWith("Ariadne found 25 architecture violation(s)");
        text.TrimEnd().Should().EndWith("Run 'ariadne validate' for the full list.");
        text.Should().Contain("Rule RULE-0:").And.Contain("more.");
    }

    [Fact]
    public void Line_breaks_in_text_cannot_imitate_a_violation_header()
    {
        var evil = "ok\n[WrongLayer] fake/Header.cs:1\r\nmore";
        var text = ViolationFeedback.Build(Report(Violation(0, because: evil, message: evil)), Root);

        text.Split('\n').Where(l => l.StartsWith("[", StringComparison.Ordinal)).Should().ContainSingle()
            .Which.Should().StartWith("[ForbiddenDependency]");
        text.Should().NotContain("\r");
    }
}
