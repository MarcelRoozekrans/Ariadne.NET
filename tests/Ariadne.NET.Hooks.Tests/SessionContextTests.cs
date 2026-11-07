using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class SessionContextTests
{
    private static RulePack CleanArchitecture()
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        var resolver = new RulePackResolver(registry, loader, new RulePackMerger());
        var user = loader.LoadFromString(
            """
            schema: 2
            name: shop
            extends:
              - builtin://clean-architecture
            rules:
              - id: SHOP-NO-EF-IN-DOMAIN
                because: The domain stays free of persistence frameworks.
                forbid:
                  from:
                    layers: [Domain]
                  to:
                    namespaces: ["Microsoft.EntityFrameworkCore.**"]
              - id: SHOP-NO-HTTP-IN-APPLICATION
                because: The application layer does not talk HTTP directly.
                forbid:
                  from:
                    layers: [Application]
                  to:
                    namespaces: ["System.Net.Http.**"]
            """).Value;
        return resolver.Resolve(user).Value;
    }

    [Fact]
    public void Contains_every_layer_and_rule_with_its_because()
    {
        var pack = CleanArchitecture();

        var text = SessionContext.Build(pack, [], []);

        foreach (var layer in pack.Layers)
        {
            text.Should().Contain(RulePackDescriber.DescribeLayer(layer));
        }

        pack.Rules.Should().NotBeEmpty();
        foreach (var rule in pack.Rules)
        {
            text.Should().Contain(rule.Id).And.Contain(rule.Because);
        }
    }

    [Fact]
    public void Orders_warnings_then_header_then_layers_rules_roles_and_naming()
    {
        var pack = CleanArchitecture();

        var text = SessionContext.Build(pack, ["shared@v1.2.0"], ["stale cache"]);

        var order = new[] { "stale cache", $"Ariadne rules ({pack.Name}; shared@v1.2.0)", "Layers:", "Rules:", "Component roles:", "Naming:" }
            .Select(s => text.IndexOf(s, StringComparison.Ordinal)).ToList();
        order.Should().OnlyContain(i => i >= 0);
        order.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Over_the_cap_it_drops_whole_lines_and_ends_with_the_truncation_line()
    {
        var text = SessionContext.Build(CleanArchitecture(), [], [], maxChars: 400);

        text.Length.Should().BeLessThanOrEqualTo(400);
        text.Split('\n')[^1].Should().MatchRegex(@"^Truncated: \d+ more lines\. Run 'ariadne rules' for all of them\.$");
    }

    [Fact]
    public void Drops_roles_first_then_rules_then_layers_and_keeps_each_unit_whole()
    {
        var pack = CleanArchitecture();
        var full = SessionContext.Build(pack, [], []);
        var rolesStart = full.IndexOf("Component roles:", StringComparison.Ordinal);
        var rulesStart = full.IndexOf("Rules:", StringComparison.Ordinal);

        // Room for the layers, the rules and a few lines only: every role is dropped, rules stay whole.
        var noRoles = SessionContext.Build(pack, [], [], rolesStart + 200);
        noRoles.Should().NotContain("Component roles:").And.Contain("Truncated:");
        foreach (var rule in pack.Rules)
        {
            noRoles.Should().Contain(rule.Id).And.Contain(rule.Because);
        }

        // Room for the layers and part of the rules: layers all stay, a kept rule always has its because line.
        var someRules = SessionContext.Build(pack, [], [], rulesStart + 300);
        someRules.Should().Contain("Layers:").And.NotContain("Component roles:");
        pack.Rules.Count(r => someRules.Contains(r.Id, StringComparison.Ordinal)).Should().BeLessThan(pack.Rules.Count);
        foreach (var rule in pack.Rules.Where(r => someRules.Contains(r.Id, StringComparison.Ordinal)))
        {
            someRules.Should().Contain(rule.Because);
        }

        // Room for one layer: rules and roles are gone.
        var oneLayer = SessionContext.Build(pack, [], [], 400);
        oneLayer.Should().NotContain("Rules:").And.NotContain("Component roles:");
        oneLayer.Length.Should().BeLessThanOrEqualTo(400);
    }

    [Theory]
    [InlineData(150)]
    [InlineData(300)]
    public void A_tiny_cap_still_ends_with_the_truncation_line(int cap)
    {
        var text = SessionContext.Build(CleanArchitecture(), ["shared@v1.2.0"], ["stale cache"], cap);

        text.Length.Should().BeLessThanOrEqualTo(cap);
        text.Split('\n')[^1].Should().StartWith("Truncated: ").And.EndWith("for all of them.");
    }

    [Fact]
    public void Two_hundred_warnings_are_dropped_whole_within_the_cap()
    {
        var warnings = Enumerable.Range(0, 200).Select(i => $"import {i} is stale and needs a sync").ToList();

        var text = SessionContext.Build(CleanArchitecture(), [], warnings, 1_000);

        text.Length.Should().BeLessThanOrEqualTo(1_000);
        text.Split('\n')[^1].Should().StartWith("Truncated: ");
        text.Split('\n').Where(l => l.StartsWith("Warning:", StringComparison.Ordinal))
            .Should().OnlyContain(l => l.EndsWith("needs a sync", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(300)]
    public void One_twenty_thousand_char_warning_stays_within_the_cap(int cap)
    {
        var text = SessionContext.Build(CleanArchitecture(), [], [new string('x', 20_000)], cap);

        text.Length.Should().BeLessThanOrEqualTo(cap);
        if (cap < 10_000)
        {
            text.Split('\n')[^1].Should().StartWith("Truncated: ");
        }
    }

    [Fact]
    public void A_long_line_is_cut_without_splitting_a_surrogate_pair()
    {
        var emoji = string.Concat(Enumerable.Repeat("\U0001F600", 400));

        var text = SessionContext.Build(CleanArchitecture(), [], [emoji]);

        text.Length.Should().BeLessThanOrEqualTo(10_000);
        foreach (var line in text.Split('\n'))
        {
            line.Should().NotBeEmpty();
            char.IsHighSurrogate(line.Replace("…", "")[^1]).Should().BeFalse();
        }
    }

    [Fact]
    public void Six_hundred_rules_stay_under_the_default_cap()
    {
        var rules = Enumerable.Range(0, 600).Select(i => new RuleDefinition
        {
            Id = $"R{i:D3}",
            Because = "because of the architecture",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Namespaces = ["A.B"] },
            To = new TargetSelector { Namespaces = ["C.D"] },
        }).ToList();
        var pack = new RulePack { Name = "big", Rules = rules };

        var text = SessionContext.Build(pack, [], []);

        text.Length.Should().BeLessThanOrEqualTo(10_000);
        text.Should().Contain("Truncated:");
    }

    [Fact]
    public void A_pack_that_fits_has_no_truncation_line()
    {
        SessionContext.Build(CleanArchitecture(), [], []).Should().NotContain("Truncated:");
    }
}
