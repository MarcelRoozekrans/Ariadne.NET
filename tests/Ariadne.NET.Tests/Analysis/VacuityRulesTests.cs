using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

public class VacuityRulesTests
{
    private const string TwoLayers = """
        namespace Shop.Domain { public class Order { } }
        namespace Shop.Web { public class Page { public Shop.Domain.Order O = null!; } }
        """;

    private static LayerDefinition Layer(string name, params string[] namespaces) =>
        new() { Name = name, Id = "L-" + name.ToUpperInvariant(), Because = "Layer " + name + ".", Namespaces = namespaces };

    private static RuleDefinition Rule(string id, RuleKind kind, TargetSelector from, bool allowEmpty = false, TargetSelector? to = null) =>
        new() { Id = id, Because = "Because " + id + ".", Kind = kind, From = from, To = to ?? new TargetSelector(), AllowEmpty = allowEmpty };

    private static RulePack Pack(IList<LayerDefinition> layers, IList<RuleDefinition>? rules = null, IList<ComponentRoleDefinition>? roles = null) =>
        new() { Name = "test", Layers = layers, Rules = rules ?? [], ComponentRoles = roles ?? [] };

    private static TargetSelector Ns(string ns) => new() { Namespaces = [ns] };

    private static TargetSelector InLayer(string layer) => new() { Layers = [layer] };

    [Fact]
    public void Forbid_rule_whose_from_matches_no_type_is_vacuous()
    {
        var pack = Pack([], [Rule("R-1", RuleKind.Forbid, Ns("Nowhere.**"), to: Ns("Shop.**"))]);

        var v = Check(pack, TwoLayers).Should().ContainSingle().Subject;
        v.Kind.Should().Be(ViolationKind.VacuousRule);
        v.RuleId.Should().Be("R-1");
        v.SourceType.Should().Be("R-1");
        v.Because.Should().Be("Because R-1.");
        v.Message.Should().Be("Rule 'R-1' selects no type: its 'from' selector matches nothing in the 1 project(s) loaded.");
    }

    [Fact]
    public void Forbid_rule_whose_to_matches_no_type_is_not_vacuous()
    {
        var pack = Pack([], [Rule("R-1", RuleKind.Forbid, Ns("Shop.**"), to: Ns("Nowhere.**"))]);

        Check(pack, TwoLayers).Should().BeEmpty();
    }

    [Fact]
    public void Allow_empty_silences_a_vacuous_rule()
    {
        var pack = Pack([], [Rule("R-1", RuleKind.Forbid, Ns("Nowhere.**"), allowEmpty: true)]);

        Check(pack, TwoLayers).Should().BeEmpty();
    }

    [Fact]
    public void Require_rule_and_unique_names_rule_check_from_only()
    {
        var pack = Pack([],
        [
            Rule("R-REQ", RuleKind.Require, Ns("Nowhere.**")),
            Rule("R-UNQ", RuleKind.UniqueNames, Ns("Nowhere.**")),
            Rule("R-ALLOW", RuleKind.AllowOnly, Ns("Nowhere.**"), to: Ns("Shop.**")),
            Rule("R-OK", RuleKind.Require, Ns("Shop.**")),
        ]);

        var violations = Check(pack, TwoLayers);

        violations.Select(v => v.RuleId).Should().Equal("R-REQ", "R-UNQ", "R-ALLOW");
        violations[0].Message.Should().Be("Rule 'R-REQ' selects no type: its 'for' selector matches nothing in the 1 project(s) loaded.");
        violations[1].Message.Should().Be("Rule 'R-UNQ' selects no type: its 'for' selector matches nothing in the 1 project(s) loaded.");
        violations[2].Message.Should().Be("Rule 'R-ALLOW' selects no type: its 'from' selector matches nothing in the 1 project(s) loaded.");
    }

    [Fact]
    public void Layer_with_no_types_is_vacuous()
    {
        var pack = Pack([Layer("Domain", "**.Domain.**"), Layer("Ghost", "**.Ghost.**"), new LayerDefinition { Name = "Empty", Because = "Nothing." }]);

        var violations = Check(pack, TwoLayers);

        violations.Select(v => v.Kind).Should().OnlyContain(k => k == ViolationKind.VacuousLayer);
        violations.Select(v => v.SourceType).Should().Equal("Ghost", "Empty");
        violations[0].RuleId.Should().Be("L-GHOST");
        violations[0].Because.Should().Be("Layer Ghost.");
        violations[0].Message.Should().Be("Layer 'Ghost' covers no type in the 1 project(s) loaded.");
        violations[1].RuleId.Should().Be("Empty");
    }

    [Fact]
    public void Layer_that_only_selects_referenced_external_types_is_not_vacuous()
    {
        var pack = Pack([Layer("Framework", "System.Collections.Generic")]);

        Check(pack, "namespace Shop { public class A { public System.Collections.Generic.List<int> L = null!; } }").Should().BeEmpty();
    }

    [Fact]
    public void Layer_whose_types_are_all_generated_is_vacuous()
    {
        var pack = Pack([Layer("Gen", "**.Gen.**")]);
        var compilation = TestCompilation.Create(("Gen.cs", "namespace Shop.Gen { public class G { } }"), ("Own.cs", "namespace Shop { public class A { } }"));
        var generated = new HashSet<SyntaxTree> { compilation.SyntaxTrees[0] };

        Check(pack, [(compilation, generated)]).Should().ContainSingle().Which.Kind.Should().Be(ViolationKind.VacuousLayer);
    }

    [Fact]
    public void Type_in_two_layers_counts_for_both()
    {
        var pack = Pack([Layer("A", "Shop.**"), Layer("B", "Shop.Domain")]);

        Check(pack, TwoLayers).Should().BeEmpty();
    }

    [Fact]
    public void Rule_selecting_only_a_vacuous_layer_gets_no_vacuous_rule()
    {
        var pack = Pack([Layer("Ghost", "**.Ghost.**")], [Rule("R-1", RuleKind.Forbid, InLayer("Ghost"), to: Ns("Shop.**"))]);

        Check(pack, TwoLayers).Should().ContainSingle().Which.Kind.Should().Be(ViolationKind.VacuousLayer);
    }

    [Fact]
    public void Role_with_no_matching_type_is_vacuous_unless_allow_empty()
    {
        var pack = Pack(
            [Layer("Domain", "**.Domain.**")],
            roles:
            [
                new ComponentRoleDefinition { Name = "Handler", Layer = "Domain" },
                new ComponentRoleDefinition { Name = "Quiet", Layer = "Domain", AllowEmpty = true },
                new ComponentRoleDefinition { Name = "Entity", Layer = "Domain" },
            ]);
        var compilation = TestCompilation.Create(TwoLayers);
        var graph = TypeGraphBuilder.Build(compilation, new HashSet<SyntaxTree>(), CancellationToken.None);
        var entity = pack.ComponentRoles[2];
        var matches = new RoleMatches(graph, [(compilation.GetTypeByMetadataName("Shop.Domain.Order")!, entity)]);

        var v = Run(pack, [graph], [matches], [Project(compilation)]).Should().ContainSingle().Subject;

        v.Kind.Should().Be(ViolationKind.VacuousRule);
        v.RuleId.Should().Be("Handler");
        v.SourceType.Should().Be("Handler");
        v.Because.Should().BeEmpty();
        v.Message.Should().Be("Role 'Handler' matches no type in layer 'Domain' in the 1 project(s) loaded.");
    }

    [Fact]
    public void Rule_and_role_naming_a_vacuous_layer_in_another_case_get_no_second_violation()
    {
        var pack = Pack(
            [Layer("Ghost", "**.Ghost.**")],
            [Rule("R-1", RuleKind.Forbid, InLayer("ghost"), to: Ns("Shop.**"))],
            [new ComponentRoleDefinition { Name = "Handler", Layer = "GHOST" }]);

        Check(pack, TwoLayers).Should().ContainSingle().Which.Kind.Should().Be(ViolationKind.VacuousLayer);
    }

    [Fact]
    public void Layer_found_under_another_case_is_not_vacuous_and_a_role_held_under_another_case_is_not_reported()
    {
        var pack = Pack(
            [Layer("Domain", "**.Domain.**")],
            [Rule("R-1", RuleKind.Forbid, InLayer("domain"), to: Ns("Shop.**"))],
            [new ComponentRoleDefinition { Name = "Entity", Layer = "domain" }]);
        var compilation = TestCompilation.Create(TwoLayers);
        var graph = TypeGraphBuilder.Build(compilation, new HashSet<SyntaxTree>(), CancellationToken.None);
        var held = new ComponentRoleDefinition { Name = "ENTITY", Layer = "Domain" };
        var matches = new RoleMatches(graph, [(compilation.GetTypeByMetadataName("Shop.Domain.Order")!, held)]);

        Run(pack, [graph], [matches], [Project(compilation)]).Should().BeEmpty();
    }

    [Fact]
    public void Role_in_a_vacuous_layer_gets_no_vacuous_rule()
    {
        var pack = Pack([Layer("Ghost", "**.Ghost.**")], roles: [new ComponentRoleDefinition { Name = "Handler", Layer = "Ghost" }]);

        Check(pack, TwoLayers).Should().ContainSingle().Which.Kind.Should().Be(ViolationKind.VacuousLayer);
    }

    [Fact]
    public void Banned_packages_with_empty_from_is_never_vacuous()
    {
        var pack = Pack([], [Rule("R-PKG", RuleKind.BannedPackages, new TargetSelector())]);

        Check(pack, TwoLayers).Should().BeEmpty();
    }

    [Fact]
    public void Banned_packages_with_from_matching_no_project_is_vacuous()
    {
        var pack = Pack([],
        [
            Rule("R-PKG", RuleKind.BannedPackages, new TargetSelector { Assemblies = ["Other.*"] }),
            Rule("R-PKG-OK", RuleKind.BannedPackages, new TargetSelector { Assemblies = ["TestAssembly"] }),
        ]);

        var v = Check(pack, TwoLayers).Should().ContainSingle().Subject;
        v.RuleId.Should().Be("R-PKG");
        v.Message.Should().Be("Rule 'R-PKG' selects no type: its 'from.assemblies' matches none of the 1 project(s) loaded.");
    }

    [Fact]
    public void Violations_have_no_location_and_the_expected_kinds()
    {
        var pack = Pack(
            [Layer("Ghost", "**.Ghost.**")],
            [Rule("R-1", RuleKind.Forbid, Ns("Nowhere.**"))],
            [new ComponentRoleDefinition { Name = "Handler", Layer = "Domain" }]);

        var violations = Check(pack, TwoLayers);

        violations.Select(v => v.Kind).Should().Equal(ViolationKind.VacuousLayer, ViolationKind.VacuousRule, ViolationKind.VacuousRule);
        violations.Should().OnlyContain(v => v.Location == null && v.Target == null);
        ((int)ViolationKind.VacuousRule).Should().Be(11);
        ((int)ViolationKind.VacuousLayer).Should().Be(12);
    }

    private static AnalyzedProject Project(CSharpCompilation compilation) => new("Shop", null, compilation);

    private static IReadOnlyList<ArchitectureViolation> Check(RulePack pack, string source) =>
        Check(pack, [(TestCompilation.Create(source), new HashSet<SyntaxTree>())]);

    private static IReadOnlyList<ArchitectureViolation> Check(RulePack pack, (CSharpCompilation Compilation, HashSet<SyntaxTree> Generated)[] inputs)
    {
        var graphs = inputs.Select(i => TypeGraphBuilder.Build(i.Compilation, i.Generated, CancellationToken.None)).ToList();
        return Run(pack, graphs, [], inputs.Select(i => Project(i.Compilation)).ToList());
    }

    private static IReadOnlyList<ArchitectureViolation> Run(
        RulePack pack, IReadOnlyList<TypeGraph> graphs, IReadOnlyList<RoleMatches> roleMatches, IReadOnlyList<AnalyzedProject> projects) =>
        VacuityRules.Check(pack, new LayerMap(pack), graphs, roleMatches, projects);
}
