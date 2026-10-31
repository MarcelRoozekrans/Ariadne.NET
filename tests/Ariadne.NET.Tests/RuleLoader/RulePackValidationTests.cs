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
    public void Layer_with_a_name_loads()
    {
        var result = _loader.LoadFromString("""
            name: p
            layers:
              - name: Domain
            """);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Empty_layer_name_is_invalid_and_the_detail_names_the_path()
    {
        var result = _loader.LoadFromString("""
            name: p
            layers:
              - name: ""
            """);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Rule pack 'p' is invalid.");
        result.Error.Detail.Should().Contain("Layers[0].Name");
    }

    [Fact]
    public void Missing_layer_name_is_invalid()
    {
        var result = _loader.LoadFromString("""
            name: p
            layers:
              - description: no name
            """);

        result.Error.Detail.Should().Contain("Layers[0].Name");
    }

    [Fact]
    public void Role_without_name_or_layer_is_invalid()
    {
        var result = _loader.LoadFromString("""
            name: p
            componentRoles:
              - name: ""
                layer: ""
            """);

        result.Error.Detail.Should().Contain("ComponentRoles[0].Name").And.Contain("ComponentRoles[0].Layer");
    }

    [Fact]
    public void Code_feature_rule_without_an_id_is_invalid()
    {
        var result = _loader.LoadFromString("""
            name: p
            codeFeatureRules:
              - id: ""
                description: d
            """);

        result.Error.Detail.Should().Contain("CodeFeatureRules[0].Id");
    }

    [Fact]
    public void Every_failure_is_listed_on_its_own_line()
    {
        var result = _loader.LoadFromString("""
            name: p
            layers:
              - name: ""
              - name: ""
            """);

        result.Error.Detail!.Split('\n').Should().HaveCount(2)
            .And.OnlyContain(line => line.Contains(": "));
    }

    [Fact]
    public void Pack_without_a_name_loads_because_extends_only_files_have_none()
    {
        var result = _loader.LoadFromString("""
            extends:
              - builtin://clean-architecture
            """);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Partial_user_pack_loads()
    {
        const string Yaml = """
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
    public void LoadFromFile_reports_member_rule_failures()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ariadne-validation-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, "name: p\nlayers:\n  - name: \"\"\n");

        try
        {
            var result = _loader.LoadFromFile(path);

            result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
            result.Error.Detail.Should().Contain("Layers[0].Name");
        }
        finally
        {
            File.Delete(path);
        }
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
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
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
    public void Nameless_pack_from_a_file_failing_a_member_rule_names_the_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ariadne-nameless-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, "layers:\n  - name: \"\"\n");

        try
        {
            var result = _loader.LoadFromFile(path);

            result.Error.Message.Should().Be($"Rule pack file '{path}' is invalid.");
            result.Error.Detail.Should().Contain("Layers[0].Name");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Nameless_pack_from_a_string_failing_a_member_rule_says_yaml()
    {
        var result = _loader.LoadFromString("layers:\n  - name: \"\"\n");

        result.Error.Message.Should().Be("Rule pack YAML is invalid.");
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
    [InlineData("name: p\nextends: ~\n", "Extends: Extends must not be null.")]
    [InlineData("name: p\nextends:\n  - ~\n", "Extends: Extends must not contain null entries.")]
    [InlineData("name: p\nlayers: ~\n", "Layers: Layers must not be null.")]
    [InlineData("name: p\nlayers:\n  - ~\n", "Layers: Layers must not contain null entries.")]
    [InlineData("name: p\ncomponentRoles: ~\n", "ComponentRoles: ComponentRoles must not be null.")]
    [InlineData("name: p\ncomponentRoles:\n  - ~\n", "ComponentRoles: ComponentRoles must not contain null entries.")]
    [InlineData("name: p\ncodeFeatureRules: ~\n", "CodeFeatureRules: CodeFeatureRules must not be null.")]
    [InlineData("name: p\ncodeFeatureRules:\n  - ~\n", "CodeFeatureRules: CodeFeatureRules must not contain null entries.")]
    [InlineData("name: p\nnamingConventions: ~\n", "NamingConventions: NamingConventions must not be null.")]
    [InlineData("name: p\nlayers:\n  - name: A\n    allowedDependencies: ~\n", "Layers[0].AllowedDependencies: AllowedDependencies must not be null.")]
    [InlineData("name: p\nlayers:\n  - name: A\n    allowedDependencies:\n      - ~\n", "Layers[0].AllowedDependencies: AllowedDependencies must not contain null entries.")]
    [InlineData("name: p\ncomponentRoles:\n  - name: R\n    layer: L\n    alternativeNames: ~\n", "ComponentRoles[0].AlternativeNames: AlternativeNames must not be null.")]
    [InlineData("name: p\ncomponentRoles:\n  - name: R\n    layer: L\n    alternativeNames:\n      - ~\n", "ComponentRoles[0].AlternativeNames: AlternativeNames must not contain null entries.")]
    [InlineData("name: p\ncomponentRoles:\n  - name: R\n    layer: L\n    accessibility: ~\n", "ComponentRoles[0].Accessibility: Accessibility must not be null.")]
    [InlineData("name: p\nnamingConventions:\n  namespace: ~\n", "NamingConventions.Namespace: Namespace must not be empty.")]
    [InlineData("name: p\nnamingConventions:\n  filePath: ~\n", "NamingConventions.FilePath: FilePath must not be empty.")]
    public void Null_yaml_members_are_invalid_and_never_throw(string yaml, string expectedLine)
    {
        var loaded = _loader.LoadFromString(yaml);

        loaded.IsFailure.Should().BeTrue();
        loaded.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        loaded.Error.Message.Should().Be("Rule pack 'p' is invalid.");
        loaded.Error.Detail!.Split('\n').Should().Contain(expectedLine);
    }

    [Fact]
    public void Null_and_member_failures_come_back_in_one_error()
    {
        var result = _loader.LoadFromString("name: p\nlayers:\n  - name: \"\"\ncomponentRoles: ~\n");

        result.Error.Detail!.Split('\n').Should().HaveCount(2);
        result.Error.Detail.Should().Contain("Layers[0].Name").And.Contain("ComponentRoles");
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
}
