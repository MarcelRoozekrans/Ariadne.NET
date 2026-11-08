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
    public void No_builtin_pack_rule_or_layer_sets_allow_empty(string packName)
    {
        var pack = Load(packName);

        pack.Rules.Where(r => r.AllowEmpty).Select(r => r.Id)
            .Should().BeEmpty($"pack '{packName}' must not exempt a rule from vacuity detection");

        // LayerDefinition has no AllowEmpty property, so a layer that tried to opt out can only show in the YAML text.
        var yaml = File.ReadAllText(Path.Combine(BuiltinPackDirectory(), packName + ".yaml"));
        var layers = yaml.Split('\n')
            .SkipWhile(l => !l.StartsWith("layers:", StringComparison.Ordinal))
            .Skip(1)
            .TakeWhile(l => l.Length == 0 || char.IsWhiteSpace(l[0]) || l[0] == '#')
            .Where(l => l.Contains("allowEmpty", StringComparison.OrdinalIgnoreCase));
        layers.Should().BeEmpty($"pack '{packName}' must not exempt a layer from vacuity detection");
    }

    [Theory]
    [MemberData(nameof(PackNames))]
    public void Every_builtin_pack_validates_a_single_type_solution_without_a_vacuous_rule_violation(string packName)
    {
        var pack = Load(packName);
        var project = new AnalyzedProject(
            "Solo", null, TestCompilation.Create("namespace Solo { public class Lonely { } }").WithAssemblyName("Solo"));

        var result = new ArchitectureValidator().Validate(pack, [project]);

        // A layer that selects nothing in a one-type solution is expected; a vacuous rule or role is not.
        result.Violations.Where(v => v.Kind == ViolationKind.VacuousRule)
            .Select(v => $"{v.RuleId}: {v.Message}")
            .Should().BeEmpty($"pack '{packName}' must stay quiet about empty rules and roles");
    }

    private static string BuiltinPackDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "src", "Ariadne.NET.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        dir.Should().NotBeNull("the repository root must be reachable from the test output directory");
        return Path.Combine(dir!, "src", "Ariadne.NET", "BuiltinPacks");
    }
}
