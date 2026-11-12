using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;
using FluentAssertions;

namespace Ariadne.NET.Tests.RuleLoader;

public class PackYamlReaderSlicesTests
{
    // The rule lines start on line 6, indented by four spaces.
    private const string Header = "schema: 2\nname: shop\nrules:\n  - id: R1\n    because: x\n";

    private const string Capture = "a capture, (*) or (**), is only accepted in the 'matching' of a slices rule";

    [Fact]
    public void Reads_independent_rule()
    {
        var yaml = """
            schema: 2
            name: shop
            rules:
              - id: SHOP-MODULES-INDEPENDENT
                because: A module reaches another module only through its public API.
                slices:
                  matching: ["Shop.(*).**"]
                  independent: true
                  allow: { namespaces: ["Shop.*.Public.**"] }
            """;

        var rule = Read(yaml);

        rule.Id.Should().Be("SHOP-MODULES-INDEPENDENT");
        rule.Kind.Should().Be(RuleKind.Slices);
        rule.Slices.Should().NotBeNull();
        rule.Slices!.Matching.Should().Equal("Shop.(*).**");
        rule.Slices.Independent.Should().BeTrue();
        rule.Slices.FreeOfCycles.Should().BeFalse();
        rule.Slices.Allow!.Namespaces.Should().Equal("Shop.*.Public.**");
        rule.From.Should().BeEquivalentTo(new TargetSelector());
        rule.To.Should().BeEquivalentTo(new TargetSelector());
        rule.Against.Should().BeEquivalentTo(new TargetSelector());
        rule.Exceptions.Should().BeEmpty();
    }

    [Fact]
    public void Reads_freeOfCycles_rule_with_exceptions()
    {
        var yaml = """
            schema: 2
            name: shop
            rules:
              - id: SHOP-MODULES-ACYCLIC
                because: Modules form a hierarchy.
                allowEmpty: true
                slices:
                  matching: ["Shop.(**).Core", "**.(*).Public.**"]
                  freeOfCycles: true
                  allow: { layers: [Public], sealed: true }
                exceptions:
                  - types: ["Shop.Orders.Legacy.**"]
                    because: Removed in the next release.
            """;

        var rule = Read(yaml);

        rule.Kind.Should().Be(RuleKind.Slices);
        rule.AllowEmpty.Should().BeTrue();
        rule.Slices!.Matching.Should().Equal("Shop.(**).Core", "**.(*).Public.**");
        rule.Slices.FreeOfCycles.Should().BeTrue();
        rule.Slices.Independent.Should().BeFalse();
        rule.Slices.Allow!.Layers.Should().Equal("Public");
        rule.Slices.Allow.Sealed.Should().BeTrue();
        rule.Exceptions.Should().ContainSingle().Which.Types.Should().Equal("Shop.Orders.Legacy.**");
    }

    [Fact]
    public void Slices_rule_without_allow_has_null_allow()
    {
        var rule = Read(Header + "    slices:\n      matching: [\"Shop.(*)\"]\n      freeOfCycles: true\n");

        rule.Slices!.Allow.Should().BeNull();
    }

    [Fact]
    public void Capture_outside_a_slices_rule_is_an_ordinary_glob()
    {
        // Captures belong to slices; elsewhere '(*)' stays the literal glob segment it always was, so existing packs still load.
        var rule = Read(Header + "    forbid:\n      from: { namespaces: [\"Shop.(*)\"] }\n      to: { namespaces: [\"System.**\"] }\n");

        rule.From.Namespaces.Should().Equal("Shop.(*)");
    }

    public static TheoryData<string, int, int, string> Rejected => new()
    {
        // No capture, two captures, and a capture that is not a whole segment.
        { "slices:|  matching:|    - Shop.**|  independent: true", 8, 11, "'Shop.**' in the slices of rule 'R1' is not a valid pattern: pattern 'Shop.**' must have exactly one capture, (*) or (**)" },
        { "slices:|  matching:|    - Shop.(*).(**)|  independent: true", 8, 11, "pattern 'Shop.(*).(**)' must have exactly one capture, (*) or (**)" },
        { "slices:|  matching:|    - Shop.Ord(*)|  independent: true", 8, 11, "capture must be a whole segment in 'Shop.Ord(*)'" },
        { "slices:|  matching:|    - Shop..(*)|  independent: true", 8, 11, "pattern 'Shop..(*)' has an empty segment" },

        // A capture anywhere but 'matching'.
        { "slices:|  matching: [\"Shop.(*).**\"]|  independent: true|  allow:|    namespaces:|      - Shop.(*).Public", 11, 13, "'Shop.(*).Public' in the allow of rule 'R1' is not a valid pattern: " + Capture },
        { "slices:|  matching: [\"Shop.(*).**\"]|  independent: true|exceptions:|  - namespaces:|      - Shop.(*).Legacy|    because: y", 11, 13, "'Shop.(*).Legacy' in exceptions[0] of rule 'R1' is not a valid pattern: " + Capture },

        // matching must be a list of non-empty patterns, and a condition must be a boolean.
        { "slices:|  matching: [\"\"]|  independent: true", 7, 18, "pattern must not be empty" },
        { "slices:|  matching: \"Shop.(*)\"|  independent: true", 7, 7, "'matching' in the slices of rule 'R1' must be a list of strings" },
        { "slices:|  matching: [\"Shop.(*)\"]|  freeOfCycles: \"true\"", 8, 7, "'freeOfCycles' in the slices of rule 'R1' must be true or false" },

        // matching must hold a pattern.
        { "slices:|  matching: []|  independent: true", 7, 7, "the slices of rule 'R1' needs at least one entry in 'matching'" },
        { "slices:|  independent: true", 7, 7, "the slices of rule 'R1' needs at least one entry in 'matching'" },

        // Exactly one of the conditions, set to true.
        { "slices:|  matching: [\"Shop.(*).**\"]|  freeOfCycles: true|  independent: true", 9, 7, "the slices of rule 'R1' must have only one of 'freeOfCycles' and 'independent'; 'independent' rules out cycles except through targets that allow selects" },
        { "slices:|  matching: [\"Shop.(*).**\"]", 7, 7, "the slices of rule 'R1' needs 'freeOfCycles: true' or 'independent: true'" },
        { "slices:|  matching: [\"Shop.(*).**\"]|  independent: false", 8, 7, "'independent' in the slices of rule 'R1' must be true" },
        { "slices:|  matching: [\"Shop.(*).**\"]|  freeOfCycles: false", 8, 7, "'freeOfCycles' in the slices of rule 'R1' must be true" },

        // allow takes the keys of a forbid 'to', without members, and at least one of them.
        { "slices:|  matching: [\"Shop.(*).**\"]|  independent: true|  allow: {}", 9, 14, "the allow of rule 'R1' needs at least one of 'layers', 'namespaces'" },
        { "slices:|  matching: [\"Shop.(*).**\"]|  independent: true|  allow:|    members: [\"Shop.*.Public.**.Get\"]", 10, 9, "'members' is not accepted in the allow of rule 'R1'" },
        { "slices:|  matching: [\"Shop.(*).**\"]|  independent: true|  allow: [Shop]", 9, 7, "'allow' in the slices of rule 'R1' must be a mapping" },

        // The closed key set, the mapping itself, and one rule body.
        { "slices:|  matching: [\"Shop.(*).**\"]|  independent: true|  cycles: true", 9, 7, "unknown key 'cycles' in the slices of rule 'R1'" },
        { "slices: [Shop]", 6, 5, "'slices' in rule 'R1' must be a mapping" },
        { "forbid:|  from: { layers: [A] }|  to: { layers: [B] }|slices:|  matching: [\"Shop.(*).**\"]|  independent: true", 9, 5, "rule 'R1' must have exactly one of 'forbid', 'allowOnly', 'require', 'bannedPackages', 'uniqueNames' or 'slices'" },
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public void Rejects(string ruleLines, int line, int column, string fragment)
    {
        var yaml = Header + string.Concat(ruleLines.Split('|').Select(static l => "    " + l + "\n"));

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeFalse("the pack has problems");
        result.Error.Should().Contain(
            p => p.Line == line && p.Column == column && p.Message.Contains(fragment, StringComparison.Ordinal),
            because: string.Join("; ", result.Error.Select(p => $"{p.Line}:{p.Column}: {p.Message}")));
    }

    private static RuleDefinition Read(string yaml)
    {
        var result = PackYamlReader.Read(yaml);
        result.IsSuccess.Should().BeTrue(because: result.IsSuccess ? "" : string.Join("; ", result.Error.Select(p => $"{p.Line}:{p.Column}: {p.Message}")));
        return result.Value.Rules.Should().ContainSingle().Subject;
    }
}
