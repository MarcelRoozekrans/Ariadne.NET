using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using Ariadne.NET.Tests.Analysis;
using FluentAssertions;

namespace Ariadne.NET.Tests.Integration;

/// <summary>
/// Pins how the built-in packs meet vacuity detection: a role may be empty in any solution, while a rule or layer
/// must never opt out of the check.
/// </summary>
public sealed class BuiltinPackVacuityTests
{
    private static readonly BuiltinPackRegistry Registry = new(new YamlRulePackLoader());

    public static TheoryData<string> PackNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in Registry.GetAvailablePackNames())
        {
            data.Add(name);
        }

        return data;
    }

    private static RulePack Load(string name)
    {
        var result = Registry.GetPack(name);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? $"{result.Error.Message} {result.Error.Detail}" : "");
        return result.Value;
    }

    [Fact]
    public void The_registry_lists_the_ten_built_in_packs()
    {
        Registry.GetAvailablePackNames().Should().HaveCount(10);
    }

    [Theory]
    [MemberData(nameof(PackNames))]
    public void Every_builtin_pack_role_allows_empty(string packName)
    {
        var pack = Load(packName);

        pack.ComponentRoles.Where(r => !r.AllowEmpty).Select(r => r.Name)
            .Should().BeEmpty($"pack '{packName}' is a template, so any of its roles may match no type");
    }

    [Theory]
    [MemberData(nameof(PackNames))]
    public void No_builtin_pack_rule_sets_allow_empty(string packName)
    {
        // Layers cannot carry the key because the loader rejects it, pinned by the loader test AllowEmpty_on_a_layer_is_an_unknown_key.
        var pack = Load(packName);

        pack.Rules.Where(r => r.AllowEmpty).Select(r => r.Id)
            .Should().BeEmpty($"pack '{packName}' must not exempt a rule from vacuity detection");
    }

    [Theory]
    [MemberData(nameof(PackNames))]
    public void Every_builtin_pack_validates_a_single_type_solution_without_a_vacuous_rule_violation(string packName)
    {
        var pack = Load(packName);
        var project = new AnalyzedProject(
            "Solo", null, TestCompilation.Create("namespace Solo { public class Lonely { } }").WithAssemblyName("Solo"));

        var result = new ArchitectureValidator().Validate(pack, [project]);

        // Every layer selects nothing in a one-type solution, and each is reported exactly once.
        result.Violations.Where(v => v.Kind == ViolationKind.VacuousLayer).Select(v => v.RuleId)
            .Should().BeEquivalentTo(pack.Layers.Select(l => l.Id.Length > 0 ? l.Id : l.Name));

        // A vacuous rule or role is not expected, and the layer cascade must not be what hides one.
        result.Violations.Where(v => v.Kind == ViolationKind.VacuousRule)
            .Select(v => $"{v.RuleId}: {v.Message}")
            .Should().BeEmpty($"pack '{packName}' must stay quiet about empty rules and roles");
    }

    [Theory]
    [MemberData(nameof(PackNames))]
    public void Every_builtin_pack_has_no_vacuous_rule_or_layer_when_each_layer_holds_a_type(string packName)
    {
        var pack = Load(packName);
        var sources = pack.Layers.Select(l =>
        {
            // Every built-in layer selects one namespace of the form **.Name.**, so Anchor.Name.Anchor is inside it.
            var inner = l.Namespaces[0].Replace("**.", "", StringComparison.Ordinal).Replace(".**", "", StringComparison.Ordinal);
            return ($"{inner}Anchor.cs", $"namespace Anchor.{inner}.Anchor {{ public sealed class {inner}Anchor {{ }} }}");
        }).ToArray();
        var project = new AnalyzedProject("Anchors", null, TestCompilation.Create(sources).WithAssemblyName("Anchors"));

        var result = new ArchitectureValidator().Validate(pack, [project]);

        result.Violations.Where(v => v.Kind is ViolationKind.VacuousRule or ViolationKind.VacuousLayer)
            .Select(v => $"{v.Kind} {v.RuleId}: {v.Message}")
            .Should().BeEmpty($"every layer of pack '{packName}' holds a type, so only a rule or role that opted out can be quiet");
    }
}
