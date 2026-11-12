using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;
using FluentAssertions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Tests.RuleLoader;

public class RulePackValidationTests
{
    private readonly YamlRulePackLoader _loader = new();

    private static RulePackResolver NewResolver() =>
        new(new BuiltinPackRegistry(new YamlRulePackLoader()), new YamlRulePackLoader(), new Ariadne.NET.RuleEngine.RulePackMerger());

    [Fact]
    public void Layer_with_a_name_id_because_and_selector_loads()
    {
        var result = _loader.LoadFromString("""
            schema: 2
            name: p
            layers:
              - name: Domain
                id: P-LAYER-DOMAIN
                because: The domain holds the business rules.
                namespaces: ["**.Domain.**"]
            """);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Empty_layer_name_is_invalid_and_the_detail_names_the_line()
    {
        var result = _loader.LoadFromString("""
            schema: 2
            name: p
            layers:
              - name: ""
                id: P-LAYER-X
                because: b
                namespaces: ["**.X.**"]
            """);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Rule pack YAML has 1 problem(s).");
        result.Error.Detail.Should().Be("rule pack YAML:4:5: layers[0] has no 'name'");
    }

    [Fact]
    public void Missing_layer_name_is_invalid()
    {
        var result = _loader.LoadFromString("""
            schema: 2
            name: p
            layers:
              - description: no name
                id: P-LAYER-X
                because: b
                namespaces: ["**.X.**"]
            """);

        result.Error.Detail.Should().Be("rule pack YAML:4:5: layers[0] has no 'name'");
    }

    [Fact]
    public void Role_without_name_or_layer_is_invalid()
    {
        var result = _loader.LoadFromString("""
            schema: 2
            name: p
            componentRoles:
              - name: ""
                layer: ""
            """);

        result.Error.Detail.Should().Contain("componentRoles[0] has no 'name'").And.Contain("componentRoles[0] has no 'layer'");
    }

    [Fact]
    public void Every_failure_is_listed_on_its_own_line()
    {
        var result = _loader.LoadFromString("""
            schema: 2
            name: p
            layers:
              - name: ""
                id: P-LAYER-A
                because: b
                namespaces: ["**.A.**"]
              - name: ""
                id: P-LAYER-B
                because: b
                namespaces: ["**.B.**"]
            """);

        result.Error.Detail!.Split('\n').Should().HaveCount(2)
            .And.OnlyContain(line => line.StartsWith("rule pack YAML:", StringComparison.Ordinal));
    }

    [Fact]
    public void Pack_without_a_name_is_invalid()
    {
        var result = _loader.LoadFromString("""
            schema: 2
            extends:
              - builtin://clean-architecture
            """);

        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Detail.Should().Be("rule pack YAML:1:1: the pack has no 'name'");
    }

    [Fact]
    public void Partial_user_pack_loads()
    {
        const string Yaml = """
            schema: 2
            name: partial
            extends:
              - builtin://clean-architecture
            componentRoles:
              - name: Specification
                layer: Domain
            """;

        var loaded = _loader.LoadFromString(Yaml);
        loaded.IsSuccess.Should().BeTrue("the layer comes from extends, so loading checks structure only");

        var resolved = NewResolver().Resolve(loaded.Value);
        resolved.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void LoadFromFile_reports_problems_with_the_path()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ariadne-validation-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, "schema: 2\nname: p\nlayers:\n  - name: \"\"\n    id: P-X\n    because: b\n    namespaces: [\"**.X.**\"]\n");

        try
        {
            var result = _loader.LoadFromFile(path);

            result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
            result.Error.Message.Should().Be($"Rule pack file '{path}' has 1 problem(s).");
            result.Error.Detail.Should().Be($"{path}:4:5: layers[0] has no 'name'");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Empty_naming_convention_is_caught_by_the_member_rules()
    {
        // The reader accepts an empty string; the ZeroAlloc.Validation backstop on the model still rejects it.
        var result = _loader.LoadFromString("schema: 2\nname: p\nnamingConventions:\n  namespace: \"\"\n");

        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Rule pack 'p' is invalid.");
        result.Error.Detail.Should().Be("NamingConventions.Namespace: Namespace must not be empty.");
    }

    public static TheoryData<string> BuiltinPackNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in new BuiltinPackRegistry(new YamlRulePackLoader()).GetAvailablePackNames())
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(BuiltinPackNames))]
    public void Every_builtin_pack_resolves_valid(string packName)
    {
        var result = NewResolver().Resolve(new RulePack { Name = "user", Extends = [$"builtin://{packName}"] });

        result.IsSuccess.Should().BeTrue(result.IsFailure ? $"{result.Error.Message} {result.Error.Detail}" : "");
    }

    public static TheoryData<string> YamlFixtures()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "tests", "Ariadne.NET.Tests")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        dir.Should().NotBeNull("the repository root must be reachable from the test output directory");

        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(dir!, "tests"), "*.yaml", SearchOption.AllDirectories)
                     .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         // Deliberately broken packs: PackSchema.InvalidCorpusTests asserts that they fail to load or resolve.
                         && !f.Contains($"{Path.DirectorySeparatorChar}InvalidPacks{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}KnownGaps{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}ShapeGaps{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            data.Add(file);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(YamlFixtures))]
    public void Every_yaml_fixture_loads(string path)
    {
        var loaded = _loader.LoadFromFile(path);

        loaded.IsSuccess.Should().BeTrue(loaded.IsFailure ? $"{loaded.Error.Message} {loaded.Error.Detail}" : "");
        NewResolver().Resolve(loaded.Value).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Nameless_pack_from_a_file_names_the_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ariadne-nameless-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, "schema: 2\n");

        try
        {
            var result = _loader.LoadFromFile(path);

            result.Error.Message.Should().Be($"Rule pack file '{path}' has 1 problem(s).");
            result.Error.Detail.Should().Be($"{path}:1:1: the pack has no 'name'");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Nameless_pack_from_a_string_says_yaml()
    {
        var result = _loader.LoadFromString("schema: 2\n");

        result.Error.Message.Should().Be("Rule pack YAML has 1 problem(s).");
    }

    [Fact]
    public void Nameless_merged_pack_failing_a_reference_rule_has_a_clean_label()
    {
        var pack = new RulePack
        {
            Name = "",
            Extends = ["builtin://clean-architecture"],
            ComponentRoles = [new ComponentRoleDefinition { Name = "X", Layer = "Ghost" }],
        };

        var result = NewResolver().Resolve(pack);

        result.Error.Message.Should().Be("Rule pack 'clean-architecture' is invalid.")
            .And.NotContain("''").And.NotContain("+'");
    }

    [Fact]
    public void Nameless_pack_without_extends_failing_a_reference_rule_has_no_quoted_name()
    {
        var pack = new RulePack
        {
            Name = "",
            Layers = [new LayerDefinition { Name = "A", AllowedDependencies = ["Ghost"] }],
        };

        NewResolver().Resolve(pack).Error.Message.Should().Be("Rule pack is invalid.");
    }

    [Theory]
    [InlineData("extends: ~\n", "3:1: 'extends' in the pack must be a list of strings")]
    [InlineData("extends:\n  - ~\n", "3:1: 'extends' in the pack must be a list of strings")]
    [InlineData("layers: ~\n", "3:1: 'layers' in the pack must be a list")]
    [InlineData("layers:\n  - ~\n", "4:5: 'layers[0]' in the pack must be a mapping")]
    [InlineData("componentRoles: ~\n", "3:1: 'componentRoles' in the pack must be a list")]
    [InlineData("componentRoles:\n  - ~\n", "4:5: 'componentRoles[0]' in the pack must be a mapping")]
    [InlineData("namingConventions: ~\n", "3:1: 'namingConventions' in the pack must be a mapping")]
    [InlineData("layers:\n  - name: A\n    id: P-A\n    because: b\n    namespaces: [\"**.A.**\"]\n    allowedDependencies: ~\n", "8:5: 'allowedDependencies' in layer 'A' must be a list of strings")]
    [InlineData("layers:\n  - name: A\n    id: P-A\n    because: b\n    namespaces: [\"**.A.**\"]\n    allowedDependencies:\n      - ~\n", "8:5: 'allowedDependencies' in layer 'A' must be a list of strings")]
    [InlineData("componentRoles:\n  - name: R\n    layer: L\n    alternativeNames: ~\n", "6:5: 'alternativeNames' in role 'R' must be a list of strings")]
    [InlineData("componentRoles:\n  - name: R\n    layer: L\n    alternativeNames:\n      - ~\n", "6:5: 'alternativeNames' in role 'R' must be a list of strings")]
    [InlineData("componentRoles:\n  - name: R\n    layer: L\n    accessibility: ~\n", "6:5: 'accessibility' in role 'R' must be a string")]
    [InlineData("namingConventions:\n  namespace: ~\n", "4:3: 'namespace' in the namingConventions must be a string")]
    [InlineData("namingConventions:\n  filePath: ~\n", "4:3: 'filePath' in the namingConventions must be a string")]
    public void Null_yaml_members_are_invalid_and_never_throw(string body, string expectedProblem)
    {
        var loaded = _loader.LoadFromString("schema: 2\nname: p\n" + body);

        loaded.IsFailure.Should().BeTrue();
        loaded.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        loaded.Error.Message.Should().Be("Rule pack YAML has 1 problem(s).");
        loaded.Error.Detail.Should().Be("rule pack YAML:" + expectedProblem);
    }

    [Fact]
    public void Problems_in_several_entries_come_back_in_one_error()
    {
        var result = _loader.LoadFromString("schema: 2\nname: p\nlayers:\n  - name: \"\"\n    id: P-A\n    because: b\n    namespaces: [\"**.A.**\"]\ncomponentRoles: ~\n");

        result.Error.Message.Should().Be("Rule pack YAML has 2 problem(s).");
        result.Error.Detail!.Split('\n').Should().Equal(
            "rule pack YAML:4:5: layers[0] has no 'name'",
            "rule pack YAML:8:1: 'componentRoles' in the pack must be a list");
    }

    [Fact]
    public void Clean_architecture_cqrs_and_ddd_tactical_combine()
    {
        var result = NewResolver().Resolve(new RulePack
        {
            Name = "my-project",
            Extends = ["builtin://clean-architecture", "builtin://cqrs", "builtin://ddd-tactical"],
        });

        result.IsSuccess.Should().BeTrue(result.IsFailure ? $"{result.Error.Message} {result.Error.Detail}" : "");
    }

    [Fact]
    public void Role_with_a_named_type_kind_loads()
    {
        var result = _loader.LoadFromString("""
            schema: 2
            name: p
            componentRoles:
              - name: Command
                layer: Application
                typeKind: RecordStruct
            """);

        result.IsSuccess.Should().BeTrue();
        result.Value.ComponentRoles[0].TypeKind.Should().Be(CSharpTypeKind.RecordStruct);
    }

    [Fact]
    public void Role_with_an_undefined_type_kind_is_invalid()
    {
        var result = _loader.LoadFromString("""
            schema: 2
            name: p
            componentRoles:
              - name: Command
                layer: Application
                typeKind: 99
            """);

        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Rule pack YAML has 1 problem(s).");
        result.Error.Detail.Should().Be(
            "rule pack YAML:6:5: typeKind '99' in role 'Command' must be one of Class, Interface, Record, Struct, RecordStruct");
    }

    [Theory]
    [InlineData(RuleKind.Require, false)]
    [InlineData(RuleKind.BannedPackages, false)]
    [InlineData(RuleKind.Slices, false)]
    [InlineData(RuleKind.Forbid, true)]
    [InlineData(RuleKind.AllowOnly, true)]
    [InlineData(RuleKind.UniqueNames, true)]
    public void Validator_rejects_requireTarget_on_require(RuleKind kind, bool valid)
    {
        var validator = new RuleDefinitionValidator(
            new TargetSelectorValidator(),
            new TargetSelectorValidator(),
            new RuleRequirementValidator(new TargetSelectorValidator(), new TargetSelectorValidator(), new TargetSelectorValidator()),
            new TargetSelectorValidator(),
            new RuleExceptionValidator(),
            new SliceDefinitionValidator(new TargetSelectorValidator()));
        var rule = new RuleDefinition
        {
            Id = "R",
            Because = "b",
            Kind = kind,
            From = new TargetSelector(),
            Slices = kind == RuleKind.Slices ? new SliceDefinition { Matching = ["Shop.(*)"], FreeOfCycles = true } : null,
            RequireTarget = true,
        };

        var result = validator.Validate(rule);

        if (valid)
        {
            result.IsValid.Should().BeTrue();
            return;
        }

        result.IsValid.Should().BeFalse();
        var failures = result.Failures.ToArray();
        failures.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(RuleDefinition.RequireTarget));
        failures[0].ErrorMessage.Should().Contain("applies only to forbid, allowOnly and uniqueNames rules.");
    }

    [Fact]
    public void Validator_accepts_a_rule_without_requireTarget_of_any_kind()
    {
        var validator = new RuleDefinitionValidator(
            new TargetSelectorValidator(),
            new TargetSelectorValidator(),
            new RuleRequirementValidator(new TargetSelectorValidator(), new TargetSelectorValidator(), new TargetSelectorValidator()),
            new TargetSelectorValidator(),
            new RuleExceptionValidator(),
            new SliceDefinitionValidator(new TargetSelectorValidator()));

        validator.Validate(new RuleDefinition { Id = "R", Because = "b", Kind = RuleKind.Require, From = new TargetSelector() })
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Merged_rule_keeps_requireTarget()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-require-target-{Guid.NewGuid():N}");
        var cacheDir = Path.Combine(tempDir, ".architecture", ".cache", "git", "parent");
        Directory.CreateDirectory(cacheDir);
        try
        {
            File.WriteAllText(Path.Combine(cacheDir, "parent.yaml"), """
                schema: 2
                name: parent
                rules:
                  - id: P-SEAM
                    because: Agents reach Thalos through one seam.
                    allowOnly:
                      from: { namespaces: ["Shop.Agents.**"] }
                      to: { types: ["Thalos.ISubagentRunner"] }
                    requireTarget: true
                  - id: P-PLAIN
                    because: The domain stays free of Thalos.
                    forbid:
                      from: { namespaces: ["Shop.Domain.**"] }
                      to: { namespaces: ["Thalos.**"] }
                    requireTarget: true
                """);
            var child = _loader.LoadFromString("""
                schema: 2
                name: child
                extends:
                  - import://parent
                rules:
                  - id: P-SEAM
                    exceptions:
                      - types: ["Shop.Agents.Legacy"]
                        because: Legacy runs its own agents until it is retired.
                """);
            child.IsSuccess.Should().BeTrue(child.IsSuccess ? "" : child.Error.Detail);

            var resolved = NewResolver().Resolve(child.Value, tempDir);

            resolved.IsSuccess.Should().BeTrue();
            resolved.Value.Rules.Should().HaveCount(2);
            resolved.Value.Rules.Should().OnlyContain(r => r.RequireTarget);
            resolved.Value.Rules[0].Id.Should().Be("P-SEAM");
            resolved.Value.Rules[0].Exceptions.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
