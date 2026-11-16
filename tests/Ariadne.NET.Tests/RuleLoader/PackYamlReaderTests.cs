using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;
using FluentAssertions;

namespace Ariadne.NET.Tests.RuleLoader;

public class PackYamlReaderTests
{
    private const string V1Hint =
        "'schema: 2' is required; this looks like a v1 pack. See https://marcelroozekrans.github.io/Ariadne.NET/how-it-works/rule-schema#migrating-from-v1";

    private const string EveryField = """
        schema: 2
        name: shop
        version: "2.1.0"
        author: shop-team
        extends:
          - builtin://clean-architecture
        layers:
          - name: Domain
            id: SH-DOMAIN
            because: The domain holds the business rules.
            description: Entities and value objects.
            namespaces: ["**.Domain.**"]
            assemblies: ["Shop.Domain"]
            allowedDependencies: []
          - name: Application
            namespaces: ["**.UseCases.**"]
        rules:
          - id: SH-001
            because: The domain stays free of EF Core.
            description: No EF Core in the domain.
            forbid:
              from:
                layers: [Domain]
              to:
                namespaces: ["Microsoft.EntityFrameworkCore.**"]
                assemblies: ["Microsoft.EntityFrameworkCore*"]
                types: ["**.DbContext"]
            exceptions:
              - namespaces: ["Shop.Domain.Legacy"]
                assemblies: ["Shop.Legacy"]
                types: ["**.LegacyContext"]
                because: Removed in the next release.
            locked: true
          - id: SH-002
            because: Agents only use the runner.
            allowOnly:
              from:
                namespaces: ["**.Agents.**"]
              to:
                types: ["**.IRunner"]
          - id: CA-001
            exceptions:
              - types: ["**.Bootstrap"]
                because: The composition root wires everything.
        componentRoles:
          - name: Repository
            layer: Infrastructure
            typeKind: Record
            requiresInterface: true
            interfaceRole: RepositoryInterface
            accessibility: internal
            description: Persists aggregates.
            alternativeNames: [Store]
        namingConventions:
          namespace: "{SolutionName}.{Layer}"
          filePath: "src/{Layer}/{ComponentName}.cs"
        """;

    [Fact]
    public void ValidPack_ReadsEveryField()
    {

        var result = PackYamlReader.Read(EveryField);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var pack = result.Value;
        pack.Schema.Should().Be(2);
        pack.Name.Should().Be("shop");
        pack.Version.Should().Be("2.1.0");
        pack.Author.Should().Be("shop-team");
        pack.Extends.Should().Equal("builtin://clean-architecture");
        AssertEveryLayerField(pack);
        AssertEveryRuleField(pack);
        AssertEveryRoleAndNamingField(pack);
    }

    private static void AssertEveryLayerField(RulePack pack)
    {
        var domain = pack.Layers.Should().ContainSingle().Subject;
        domain.Name.Should().Be("Domain");
        domain.Id.Should().Be("SH-DOMAIN");
        domain.Because.Should().Be("The domain holds the business rules.");
        domain.Description.Should().Be("Entities and value objects.");
        domain.Namespaces.Should().Equal("**.Domain.**");
        domain.Assemblies.Should().Equal("Shop.Domain");
        domain.AllowedDependencies.Should().BeEmpty();

        var retarget = pack.LayerRetargets.Should().ContainSingle().Subject;
        retarget.Name.Should().Be("Application");
        retarget.Namespaces.Should().Equal("**.UseCases.**");
        retarget.Assemblies.Should().BeEmpty();
    }

    private static void AssertEveryRuleField(RulePack pack)
    {
        pack.Rules.Should().HaveCount(2);
        var forbid = pack.Rules[0];
        forbid.Id.Should().Be("SH-001");
        forbid.Because.Should().Be("The domain stays free of EF Core.");
        forbid.Description.Should().Be("No EF Core in the domain.");
        forbid.Kind.Should().Be(RuleKind.Forbid);
        forbid.From.Layers.Should().Equal("Domain");
        forbid.To.Namespaces.Should().Equal("Microsoft.EntityFrameworkCore.**");
        forbid.To.Assemblies.Should().Equal("Microsoft.EntityFrameworkCore*");
        forbid.To.Types.Should().Equal("**.DbContext");
        forbid.Locked.Should().BeTrue();
        var exception = forbid.Exceptions.Should().ContainSingle().Subject;
        exception.Namespaces.Should().Equal("Shop.Domain.Legacy");
        exception.Assemblies.Should().Equal("Shop.Legacy");
        exception.Types.Should().Equal("**.LegacyContext");
        exception.Because.Should().Be("Removed in the next release.");

        var allowOnly = pack.Rules[1];
        allowOnly.Kind.Should().Be(RuleKind.AllowOnly);
        allowOnly.From.Namespaces.Should().Equal("**.Agents.**");
        allowOnly.To.Types.Should().Equal("**.IRunner");
        allowOnly.Locked.Should().BeFalse();
        allowOnly.Exceptions.Should().BeEmpty();

        var amendment = pack.RuleAmendments.Should().ContainSingle().Subject;
        amendment.Id.Should().Be("CA-001");
        amendment.Exceptions.Should().ContainSingle().Which.Types.Should().Equal("**.Bootstrap");
    }

    private static void AssertEveryRoleAndNamingField(RulePack pack)
    {
        var role = pack.ComponentRoles.Should().ContainSingle().Subject;
        role.Name.Should().Be("Repository");
        role.Layer.Should().Be("Infrastructure");
        role.TypeKind.Should().Be(CSharpTypeKind.Record);
        role.RequiresInterface.Should().BeTrue();
        role.InterfaceRole.Should().Be("RepositoryInterface");
        role.Accessibility.Should().Be("internal");
        role.Description.Should().Be("Persists aggregates.");
        role.AlternativeNames.Should().Equal("Store");

        pack.NamingConventions.Namespace.Should().Be("{SolutionName}.{Layer}");
        pack.NamingConventions.FilePath.Should().Be("src/{Layer}/{ComponentName}.cs");
    }

    [Fact]
    public void MinimalPack_KeepsDefaults()
    {
        var result = PackYamlReader.Read("schema: 2\nname: tiny\n");

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Version.Should().Be("1.0.0");
        result.Value.Author.Should().BeEmpty();
        result.Value.NamingConventions.Namespace.Should().Be(new NamingConventions().Namespace);
        result.Value.NamingConventions.FilePath.Should().Be(new NamingConventions().FilePath);
    }

    [Fact]
    public void MissingSchema_ReportsV1Hint()
    {
        var yaml = """
            name: legacy
            layers:
              - name: Domain
                allowedDependencies: []
            """;

        Problems(yaml).Should().Equal(new PackProblem(1, 1, V1Hint));
    }

    [Fact]
    public void SchemaThree_Fails()
    {
        Problems("name: next\nschema: 3\n").Should().Equal(new PackProblem(2, 1, "'schema' must be 2"));
    }

    [Fact]
    public void UnknownKey_WithSuggestion()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - name: Domain
                id: SH-DOMAIN
                because: The domain holds the business rules.
                namespaces: ["**.Domain.**"]
              - name: Core
                id: SH-CORE
                because: Core depends on the domain only.
                namespaces: ["**.Core.**"]
                allowedDependancies: [Domain]
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(12, 5, "unknown key 'allowedDependancies' in layer 'Core'; did you mean 'allowedDependencies'?"));
    }

    [Fact]
    public void UnknownKey_NoSuggestion()
    {
        Problems("schema: 2\nname: shop\nowner: someone\n").Should().Equal(
            new PackProblem(3, 1, "unknown key 'owner' in the pack"));
    }

    [Fact]
    public void UnknownKey_BeforeNameIsKnown_UsesIndex()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - id: SH-X
                because: x
                namespaces: ["**.X.**"]
                colour: red
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(4, 5, "layers[0] has no 'name'"),
            new PackProblem(7, 5, "unknown key 'colour' in layers[0]"));
    }

    [Fact]
    public void CodeFeatureRules_HasOwnMessage()
    {
        var yaml = """
            schema: 2
            name: shop
            codeFeatureRules:
              - id: ARIADNE-001
                description: x
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(3, 1, "'codeFeatureRules' is not part of schema 2; typed code-feature rules arrive in a later release"));
    }

    [Fact]
    public void RuleWithoutBecause()
    {
        var yaml = """
            schema: 2
            name: shop
            rules:
              - id: SH-001
                forbid:
                  from: { layers: [Domain] }
                  to: { namespaces: ["System.Data.**"] }
            """;

        Problems(yaml).Should().Equal(new PackProblem(4, 5, "rule 'SH-001' has no 'because'"));
    }

    [Fact]
    public void RuleWithBothForbidAndAllowOnly()
    {
        var yaml = """
            schema: 2
            name: shop
            rules:
              - id: SH-001
                because: x
                forbid:
                  from: { layers: [Domain] }
                  to: { namespaces: ["System.Data.**"] }
                allowOnly:
                  from: { layers: [Domain] }
                  to: { namespaces: ["System.**"] }
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(9, 5, "rule 'SH-001' must have exactly one of 'forbid', 'allowOnly', 'require', 'bannedPackages', 'uniqueNames' or 'slices'"));
    }

    [Fact]
    public void RuleWithNeitherForbidNorAllowOnly()
    {
        Problems("schema: 2\nname: shop\nrules:\n  - id: SH-001\n    because: x\n    locked: false\n").Should().Equal(
            new PackProblem(4, 5, "rule 'SH-001' must have exactly one of 'forbid', 'allowOnly', 'require', 'bannedPackages', 'uniqueNames' or 'slices'"));
    }

    [Fact]
    public void ForbidWithoutTo_ReportsAtForbidMapping()
    {
        var yaml = """
            schema: 2
            name: shop
            rules:
              - id: SH-001
                because: x
                forbid:
                  from: { layers: [Domain] }
            """;

        Problems(yaml).Should().Equal(new PackProblem(7, 7, "the forbid of rule 'SH-001' has no 'to'"));
    }

    [Fact]
    public void TargetMixingLayersAndNamespaces()
    {
        var yaml = """
            schema: 2
            name: shop
            rules:
              - id: SH-001
                because: x
                forbid:
                  from:
                    layers: [Domain]
                    namespaces: ["**.Domain.**"]
                  to: { namespaces: ["System.Data.**"] }
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(8, 9, "a target in rule 'SH-001' cannot mix 'layers' with 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes', 'attributeArguments' or 'onlyAttributes'"));
    }

    [Fact]
    public void EmptySelector()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - name: Domain
                id: SH-DOMAIN
                because: x
                namespaces: []
            rules:
              - id: SH-001
                because: x
                forbid:
                  from: { layers: [Domain] }
                  to: { namespaces: ["System.**"] }
                exceptions:
                  - because: y
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(4, 5, "layer 'Domain' needs at least one of 'namespaces', 'assemblies'"),
            new PackProblem(15, 9, "exceptions[0] of rule 'SH-001' needs at least one of 'namespaces', 'assemblies', 'types', 'attributes', 'attributeArguments', 'onlyAttributes', 'names', 'kinds', 'accessibility', 'assignableTo', 'sealed', 'abstract', 'static', 'nested', 'dependsOn', 'onlyDependsOn'"));
    }

    [Fact]
    public void EmptyTarget()
    {
        var yaml = """
            schema: 2
            name: shop
            rules:
              - id: SH-001
                because: x
                forbid:
                  from: { layers: [Domain] }
                  to: {}
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(8, 11, "the to of rule 'SH-001' needs at least one of 'layers', 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes', 'attributeArguments', 'onlyAttributes', 'members', 'names', 'kinds', 'accessibility', 'assignableTo', 'sealed', 'abstract', 'static', 'nested', 'dependsOn', 'onlyDependsOn'"));
    }

    [Fact]
    public void InvalidGlob()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - name: Domain
                id: SH-DOMAIN
                because: x
                namespaces: ["Shop..Domain", "Shop.Do**"]
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(7, 18, "'Shop..Domain' in layer 'Domain' is not a valid pattern: pattern 'Shop..Domain' has an empty segment"),
            new PackProblem(7, 34, "'Shop.Do**' in layer 'Domain' is not a valid pattern: '**' must be a whole segment in 'Shop.Do**'"));
    }

    [Fact]
    public void LayerRetarget_ReadAsRetarget()
    {
        var yaml = """
            schema: 2
            name: child
            extends: [builtin://clean-architecture]
            layers:
              - name: Domain
                assemblies: ["Shop.Core"]
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Layers.Should().BeEmpty();
        var retarget = result.Value.LayerRetargets.Should().ContainSingle().Subject;
        retarget.Name.Should().Be("Domain");
        retarget.Namespaces.Should().BeEmpty();
        retarget.Assemblies.Should().Equal("Shop.Core");
    }

    [Fact]
    public void LayerWithOnlyName_IsADefinitionMissingFields()
    {
        Problems("schema: 2\nname: shop\nlayers:\n  - name: Domain\n").Should().Equal(
            new PackProblem(4, 5, "layer 'Domain' has no 'id'"),
            new PackProblem(4, 5, "layer 'Domain' has no 'because'"),
            new PackProblem(4, 5, "layer 'Domain' needs at least one of 'namespaces', 'assemblies'"));
    }

    [Fact]
    public void RuleAmendment_ReadAsAmendment()
    {
        var yaml = """
            schema: 2
            name: child
            extends: [builtin://clean-architecture]
            rules:
              - id: CA-001
                exceptions:
                  - namespaces: ["Shop.Bootstrap"]
                    because: The composition root wires everything.
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules.Should().BeEmpty();
        var amendment = result.Value.RuleAmendments.Should().ContainSingle().Subject;
        amendment.Id.Should().Be("CA-001");
        var exception = amendment.Exceptions.Should().ContainSingle().Subject;
        exception.Namespaces.Should().Equal("Shop.Bootstrap");
        exception.Because.Should().Be("The composition root wires everything.");
    }

    [Fact]
    public void RetargetWithoutExtends_Fails()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - name: Domain
                namespaces: ["**.Core.**"]
            rules:
              - id: CA-001
                exceptions:
                  - types: ["**.Bootstrap"]
                    because: x
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(4, 5, "layer 'Domain' only changes selectors, which needs 'extends'"),
            new PackProblem(7, 5, "rule 'CA-001' only adds exceptions, which needs 'extends'"));
    }

    [Fact]
    public void WrongScalarType_ListExpected()
    {
        var yaml = """
            schema: 2
            name: shop
            extends: builtin://clean-architecture
            layers:
              - name: Domain
                id: SH-DOMAIN
                because: x
                namespaces: "**.Domain.**"
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(3, 1, "'extends' in the pack must be a list of strings"),
            new PackProblem(8, 5, "'namespaces' in layer 'Domain' must be a list of strings"));
    }

    [Fact]
    public void WrongShapes_NameEachExpectedType()
    {
        var yaml = """
            schema: 2
            name: [shop]
            layers: {}
            namingConventions: x
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(2, 1, "'name' in the pack must be a string"),
            new PackProblem(3, 1, "'layers' in the pack must be a list"),
            new PackProblem(4, 1, "'namingConventions' in the pack must be a mapping"));
    }

    private const string AllowEmptyPack = """
        schema: 2
        name: shop
        rules:
          - id: SH-001
            because: x
            forbid:
              from: { layers: [Domain] }
              to: { namespaces: ["System.**"] }
            allowEmpty: true
          - id: SH-002
            because: y
            forbid:
              from: { layers: [Domain] }
              to: { namespaces: ["System.**"] }
        componentRoles:
          - name: Entity
            layer: Domain
            allowEmpty: true
          - name: Service
            layer: Domain
        """;

    [Fact]
    public void Rule_with_allowEmpty_true_loads_with_AllowEmpty_true()
    {
        var result = PackYamlReader.Read(AllowEmptyPack);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules[0].AllowEmpty.Should().BeTrue();
    }

    [Fact]
    public void Role_with_allowEmpty_true_loads_with_AllowEmpty_true()
    {
        var result = PackYamlReader.Read(AllowEmptyPack);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.ComponentRoles[0].AllowEmpty.Should().BeTrue();
    }

    [Fact]
    public void Rule_and_role_default_to_AllowEmpty_false()
    {
        var result = PackYamlReader.Read(AllowEmptyPack);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules[1].AllowEmpty.Should().BeFalse();
        result.Value.ComponentRoles[1].AllowEmpty.Should().BeFalse();
    }

    [Fact]
    public void AllowEmpty_with_a_non_bool_value_is_a_pack_problem()
    {
        var yaml = """
            schema: 2
            name: shop
            componentRoles:
              - name: Entity
                layer: Domain
                allowEmpty: maybe
            """;

        Problems(yaml).Should().Equal(new PackProblem(6, 5, "'allowEmpty' in role 'Entity' must be true or false"));
    }

    [Fact]
    public void AllowEmpty_on_a_layer_is_an_unknown_key()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - name: Domain
                id: SH-DOMAIN
                because: The domain holds the business rules.
                namespaces: ["**.Domain.**"]
                allowEmpty: true
            """;

        Problems(yaml).Should().ContainSingle().Which.Message.Should().StartWith("unknown key 'allowEmpty' in layer 'Domain'");
    }

    [Fact]
    public void LockedNotBool_Fails()
    {
        var yaml = """
            schema: 2
            name: shop
            rules:
              - id: SH-001
                because: x
                forbid:
                  from: { layers: [Domain] }
                  to: { namespaces: ["System.**"] }
                locked: yes
            """;

        Problems(yaml).Should().Equal(new PackProblem(9, 5, "'locked' in rule 'SH-001' must be true or false"));
    }

    [Fact]
    public void BadTypeKind_ListsNames()
    {
        var yaml = """
            schema: 2
            name: shop
            componentRoles:
              - name: Entity
                layer: Domain
                typeKind: Enum
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(6, 5, "typeKind 'Enum' in role 'Entity' must be one of Class, Interface, Record, Struct, RecordStruct"));
    }

    [Fact]
    public void RoleWithoutLayer_Fails()
    {
        Problems("schema: 2\nname: shop\ncomponentRoles:\n  - name: Entity\n").Should().Equal(
            new PackProblem(4, 5, "role 'Entity' has no 'layer'"));
    }

    [Fact]
    public void ThreeProblems_AllReported_InDocumentOrder()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - name: Domain
                id: SH-DOMAIN
                because: x
                namespaces: ["**.Domain.**"]
                allowedDependencies: Application
            rules:
              - id: SH-001
                forbid:
                  from: { layers: [Domain] }
                  to: { namespaces: ["System.**"] }
            componentRoles:
              - name: Entity
                layer: Domain
                requiresInterface: maybe
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(8, 5, "'allowedDependencies' in layer 'Domain' must be a list of strings"),
            new PackProblem(10, 5, "rule 'SH-001' has no 'because'"),
            new PackProblem(17, 5, "'requiresInterface' in role 'Entity' must be true or false"));
    }

    [Fact]
    public void ProblemInOneEntry_KeepsReadingTheOthers()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - name: Broken
                id: SH-BROKEN
                namespaces: ["**.Broken.**"]
              - name: Fine
                id: SH-FINE
                because: x
                namespaces: ["**.Fine.**"]
                descripton: typo
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(4, 5, "layer 'Broken' has no 'because'"),
            new PackProblem(11, 5, "unknown key 'descripton' in layer 'Fine'; did you mean 'description'?"));
    }

    [Fact]
    public void DuplicateKey_ReportsSecondKeyLine()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - name: Domain
                id: SH-DOMAIN
                because: x
                namespaces: ["**.Domain.**"]
                because: y
            name: other
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(8, 5, "duplicate key 'because' in layer 'Domain'"),
            new PackProblem(9, 1, "duplicate key 'name' in the pack"));
    }

    [Fact]
    public void AnchorAndAlias_Resolve()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - name: Domain
                id: SH-DOMAIN
                because: &reason Kept apart on purpose.
                namespaces: &domain ["**.Domain.**"]
              - name: Core
                id: SH-CORE
                because: *reason
                namespaces: *domain
                allowedDependencies: [Domain]
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Layers[1].Because.Should().Be("Kept apart on purpose.");
        result.Value.Layers[1].Namespaces.Should().Equal("**.Domain.**");
    }

    [Fact]
    public void UnknownAlias_IsASingleProblem()
    {
        // YamlDotNet 16.3.0 raises this with empty marks, so the problem sits at 1:1.
        Problems("schema: 2\nname: *missing\n").Should().Equal(new PackProblem(1, 1, "The anchor 'missing' does not exists"));
    }

    [Fact]
    public void UnknownAlias_WithAVeryLongName_GivesABoundedMessage()
    {
        var anchor = new string('a', 5_000);

        var problem = Problems($"schema: 2\nname: *{anchor}\n").Should().ContainSingle().Subject;

        problem.Message.Should().Be("The anchor '" + new string('a', 68) + "...");
    }

    [Fact]
    public void CrlfAndBom_SamePositionsAsLf()
    {
        var lf = string.Join('\n', (string[])[
            "schema: 2",
            "name: shop",
            "layers:",
            "  - name: Domain",
            "    id: SH-DOMAIN",
            "    because: x",
            "    namespaces: [\"**.Domain.**\"]",
            "    namespacs: []",
            "    because: y",
            "",
        ]);
        var crlfWithBom = "\uFEFF" + lf.Replace("\n", "\r\n", StringComparison.Ordinal);

        var expected = Problems(lf);

        expected.Should().Equal(
            new PackProblem(8, 5, "unknown key 'namespacs' in layer 'Domain'; did you mean 'namespaces'?"),
            new PackProblem(9, 5, "duplicate key 'because' in layer 'Domain'"));
        Problems(crlfWithBom).Should().Equal(expected);
        Problems("\uFEFF" + lf).Should().Equal(expected);
        Problems(lf.Replace("\n", "\r\n", StringComparison.Ordinal)).Should().Equal(expected);
    }

    [Fact]
    public void BomOnMissingSchema_StillReportsLineOneColumnOne()
    {
        Problems("\uFEFFname: legacy\n").Should().Equal(new PackProblem(1, 1, V1Hint));
    }

    [Fact]
    public void RootSequence_Fails()
    {
        Problems("- schema: 2\n- name: shop\n").Should().Equal(new PackProblem(1, 1, "the pack must be a mapping"));
    }

    [Fact]
    public void RootScalar_Fails()
    {
        Problems("\n  just text\n").Should().Equal(new PackProblem(2, 3, "the pack must be a mapping"));
    }

    [Theory]
    [InlineData("# only a comment\n# and another\n")]
    [InlineData("")]
    [InlineData("\uFEFF")]
    public void CommentsOnly_Fails(string yaml)
    {
        Problems(yaml).Should().Equal(new PackProblem(1, 1, "the pack is empty"));
    }

    [Fact]
    public void TwoDocuments_Fails()
    {
        Problems("schema: 2\nname: a\n---\nschema: 2\nname: b\n").Should().Equal(
            new PackProblem(4, 1, "the pack must be a single YAML document"));
    }

    [Fact]
    public void SyntaxError_SingleProblemAtMark()
    {
        Problems("schema: 2\nname: shop\nlayers:\n  - name: [Domain\n").Should().Equal(
            new PackProblem(5, 1, "While parsing a flow sequence, did not find expected ',' or ']'."));
    }

    [Fact]
    public void SyntaxError_TabIndent_IsASingleProblem()
    {
        // YamlDotNet 16.3.0 raises this with empty marks, so the problem sits at 1:1.
        Problems("schema: 2\nname: shop\nlayers:\n\t- name: Domain\n").Should().Equal(
            new PackProblem(1, 1, "While scanning a mapping, found invalid tab as indentation."));
    }

    [Fact]
    public void DuplicateKey_UnderAnUnknownKey_IsStillReported()
    {
        var yaml = """
            schema: 2
            name: shop
            owner:
              team: a
              team: b
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(3, 1, "unknown key 'owner' in the pack"),
            new PackProblem(5, 3, "duplicate key 'team' in the pack"));
    }

    [Fact]
    public void DuplicateKey_InAMappingReachedByAlias_ReportedOnce()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - &core
                name: Core
                id: SH-CORE
                because: x
                namespaces: ["**.Core.**"]
                because: y
              - *core
            """;

        Problems(yaml).Should().Equal(new PackProblem(9, 5, "duplicate key 'because' in layer 'Core'"));
    }

    [Fact]
    public void DeepNesting_StopsAtTheLimit()
    {
        // The root mapping is level 1, so the 32nd '[' opens level 33; it sits at column 4 + 31.
        var yaml = "schema: 2\nname: shop\nx: " + new string('[', 40) + new string(']', 40) + "\n";

        Problems(yaml).Should().Equal(new PackProblem(3, 35, "the pack nests deeper than 32 levels"));
    }

    [Fact]
    public void VeryDeepNesting_IsAProblemNotACrash()
    {
        var yaml = "schema: 2\nname: shop\nx: " + new string('[', 200_000) + new string(']', 200_000) + "\n";

        Problems(yaml).Should().Equal(new PackProblem(3, 35, "the pack nests deeper than 32 levels"));
    }

    [Fact]
    public void AliasExpansion_StopsAtTheBudget()
    {
        // 100 rules, each with 100 exceptions, each with 100 namespaces: a million entries from a few kilobytes.
        const int k = 100;
        var yaml = new System.Text.StringBuilder()
            .Append("schema: 2\nname: shop\nrules:\n")
            .Append("  - &r\n    id: R1\n    because: x\n    forbid: {from: {layers: [A]}, to: {layers: [B]}}\n")
            .Append("    exceptions:\n      - &e\n        because: y\n        namespaces: [")
            .AppendJoin(", ", Enumerable.Repeat("a", k))
            .Append("]\n");
        for (var i = 1; i < k; i++)
        {
            yaml.Append("      - *e\n");
        }

        for (var i = 1; i < k; i++)
        {
            yaml.Append("  - *r\n");
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var problems = Problems(yaml.ToString());
        watch.Stop();

        problems.Should().ContainSingle()
            .Which.Message.Should().Be("the pack expands to more than 100000 entries");
        problems[0].Line.Should().Be(11, "the budget runs out inside the aliased namespaces list");
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("arguments", "[", "]")]
    [InlineData("named", "{", "}")]
    public void AliasExpansion_StopsAtTheBudgetInsideAnAttributeArgumentEntry(string key, string open, string close)
    {
        // One rule whose attributeArguments entry holds 1000 values or names, aliased 150 times.
        const int k = 1000;
        var yaml = new System.Text.StringBuilder()
            .Append("schema: 2\nname: shop\nrules:\n")
            .Append("  - &r\n    id: R1\n    because: x\n    forbid:\n      to: { layers: [B] }\n      from:\n        attributeArguments:\n")
            .Append("          - ").Append(key).Append(": ").Append(open).Append('\n');
        for (var i = 0; i < k; i++)
        {
            yaml.Append("              ").Append(string.Equals(key, "named", StringComparison.Ordinal) ? $"n{i}: v" : $"v{i}").Append(",\n");
        }

        yaml.Append("              ").Append(close).Append('\n');
        for (var i = 1; i < 150; i++)
        {
            yaml.Append("  - *r\n");
        }

        var problems = Problems(yaml.ToString());

        problems.Should().ContainSingle()
            .Which.Message.Should().Be("the pack expands to more than 100000 entries");
        problems[0].Line.Should().BeInRange(9, 9 + k, "the budget runs out inside the aliased entry");
    }

    [Fact]
    public void ProblemInANodeReachedTwiceByAlias_ReportedOnce()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - &core
                name: Core
                id: SH-CORE
                because: x
                namespaces: ["a..b"]
              - *core
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(8, 18, "'a..b' in layer 'Core' is not a valid pattern: pattern 'a..b' has an empty segment"));
    }

    [Fact]
    public void LongKey_IsCutInTheMessage()
    {
        var key = new string('k', 200);

        Problems($"schema: 2\nname: shop\n{key}: x\n").Should().Equal(
            new PackProblem(3, 1, $"unknown key '{new string('k', 80)}...' in the pack"));
    }

    [Fact]
    public void ControlCharacterInKey_IsEscapedInTheMessage()
    {
        Problems("schema: 2\nname: shop\n\"a\\e[31m\\tb\": x\n").Should().Equal(
            new PackProblem(3, 1, "unknown key 'a\\u001b[31m\\u0009b' in the pack"));
    }

    [Fact]
    public void ControlCharacterInAName_IsEscapedWhereTheNameSaysWhere()
    {
        Problems("schema: 2\nname: shop\nlayers:\n  - name: \"Do\\emain\"\n    id: L\n    because: x\n").Should().Equal(
            new PackProblem(4, 5, "layer 'Do\\u001bmain' needs at least one of 'namespaces', 'assemblies'"));
    }

    [Fact]
    public void LayersEntryNotAMapping_Fails()
    {
        Problems("schema: 2\nname: shop\nlayers: [1]\n").Should().Equal(
            new PackProblem(3, 10, "'layers[0]' in the pack must be a mapping"));
    }

    [Fact]
    public void RulesEntryNotAMapping_Fails()
    {
        Problems("schema: 2\nname: shop\nrules:\n  - SH-001\n").Should().Equal(
            new PackProblem(4, 5, "'rules[0]' in the pack must be a mapping"));
    }

    [Fact]
    public void KeyWithEmptyValue_IsNotAString()
    {
        Problems("schema: 2\nname:\n").Should().Equal(new PackProblem(2, 1, "'name' in the pack must be a string"));
    }

    [Fact]
    public void NonScalarKey_MustBeAString()
    {
        Problems("schema: 2\nname: shop\n? [a, b]\n: x\n").Should().Equal(
            new PackProblem(3, 3, "a key in the pack must be a string"));
    }

    [Fact]
    public void MergeKey_IsAnUnknownKey()
    {
        var yaml = """
            schema: 2
            name: shop
            componentRoles:
              - &entity
                name: Entity
                layer: Domain
              - <<: *entity
                name: Aggregate
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(7, 5, "unknown key '<<' in role 'Aggregate'"),
            new PackProblem(7, 5, "role 'Aggregate' has no 'layer'"));
    }

    [Fact]
    public void TypeKind_IgnoresCase()
    {
        var result = PackYamlReader.Read("schema: 2\nname: shop\ncomponentRoles:\n  - name: Entity\n    layer: Domain\n    typeKind: class\n");

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.ComponentRoles[0].TypeKind.Should().Be(CSharpTypeKind.Class);
    }

    [Fact]
    public void UnknownKeys_UnderNamingConventions()
    {
        var yaml = """
            schema: 2
            name: shop
            namingConventions:
              namespce: "{Layer}"
              owner: x
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(4, 3, "unknown key 'namespce' in the namingConventions; did you mean 'namespace'?"),
            new PackProblem(5, 3, "unknown key 'owner' in the namingConventions"));
    }

    [Theory]
    [InlineData("allowedDependancies", "allowedDependencies")]
    [InlineData("nmae", "name")]
    [InlineData("Because", "because")]
    [InlineData("assembly", null)]
    [InlineData("owner", null)]
    [InlineData("", "id")]
    public void KeySuggestion_WithinTwoEdits(string key, string? expected)
    {
        KeySuggestion.Closest(key, ["name", "id", "because", "assemblies", "allowedDependencies"]).Should().Be(expected);
    }

    [Fact]
    public void KeySuggestion_TieGoesToTheFirstListed()
    {
        KeySuggestion.Closest("ab", ["ax", "xb"]).Should().Be("ax");
        KeySuggestion.Closest("ab", ["xb", "ax"]).Should().Be("xb");
    }

    [Fact]
    public void KeySuggestion_PrefersTheLowerDistance()
    {
        KeySuggestion.Closest("types", ["typez1", "types1"]).Should().Be("types1");
    }

    private const string RequireHead = "schema: 2\nname: shop\nrules:\n  - id: SH-001\n    because: x\n";

    [Fact]
    public void Require_ReadsForLayerKindAccessibilityAndImplements()
    {
        var yaml = RequireHead + """
                require:
                  for: { interfaces: ["**.I*Repository"], baseTypes: ["**.Base"], attributes: ["**.Marker"] }
                  layer: Infrastructure
                  kind: class
                  accessibility: internal
                  implements: { types: ["**.I*Repository"], namespaces: ["Shop.**"], assemblies: ["Shop.*"] }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var rule = result.Value.Rules.Should().ContainSingle().Subject;
        rule.Kind.Should().Be(RuleKind.Require);
        rule.From.Interfaces.Should().Equal("**.I*Repository");
        rule.From.BaseTypes.Should().Equal("**.Base");
        rule.From.Attributes.Should().Equal("**.Marker");
        rule.Require.Should().NotBeNull();
        rule.Require!.Layer.Should().Be("Infrastructure");
        rule.Require.Kind.Should().Be(CSharpTypeKind.Class);
        rule.Require.Accessibility.Should().Be("internal");
        rule.Require.Implements!.Types.Should().Equal("**.I*Repository");
        rule.Require.Implements.Namespaces.Should().Equal("Shop.**");
        rule.Require.Implements.Assemblies.Should().Equal("Shop.*");
    }

    [Fact]
    public void Require_WithoutFor_ReportsLine()
    {
        var yaml = RequireHead + "    require:\n      layer: Domain\n";

        Problems(yaml).Should().Equal(new PackProblem(7, 7, "the require of rule 'SH-001' has no 'for'"));
    }

    [Fact]
    public void Require_WithNoRequirement_Fails()
    {
        var yaml = RequireHead + "    require:\n      for: { types: [\"**.X\"] }\n";

        Problems(yaml).Should().Equal(
            new PackProblem(7, 7, "the require of rule 'SH-001' needs at least one of 'layer', 'kind', 'accessibility', 'implements', 'is', 'isNot'"));
    }

    [Fact]
    public void Require_WithForbidAlsoPresent_Fails()
    {
        var yaml = RequireHead + """
                forbid:
                  from: { layers: [Domain] }
                  to: { namespaces: ["System.**"] }
                require:
                  for: { types: ["**.X"] }
                  layer: Domain
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(9, 5, "rule 'SH-001' must have exactly one of 'forbid', 'allowOnly', 'require', 'bannedPackages', 'uniqueNames' or 'slices'"));
    }

    [Theory]
    [InlineData("kind: Widget", 8, "kind 'Widget' in the require of rule 'SH-001' must be one of")]
    [InlineData("accessibility: private", 8, "accessibility 'private' in the require of rule 'SH-001' must be public or internal")]
    public void Require_KindAndAccessibilityOutsideTheirValues_Fail(string line, int at, string expected)
    {
        var yaml = RequireHead + $"    require:\n      for: {{ types: [\"**.X\"] }}\n      {line}\n";

        var problem = Problems(yaml).Should().ContainSingle().Subject;
        problem.Line.Should().Be(at);
        problem.Message.Should().StartWith(expected);
    }

    [Theory]
    [InlineData("class")]
    [InlineData("Class")]
    public void Require_Kind_IsMatchedCaseInsensitively(string kind)
    {
        var yaml = RequireHead + $"    require:\n      for: {{ types: [\"**.X\"] }}\n      kind: {kind}\n";

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules.First().Require!.Kind.Should().Be(CSharpTypeKind.Class);
    }

    [Fact]
    public void Require_WithEmptyLayer_FailsAtTheKey()
    {
        var yaml = RequireHead + "    require:\n      for: { types: [\"**.X\"] }\n      layer:\n";

        Problems(yaml).Should().Equal(new PackProblem(8, 7, "'layer' in the require of rule 'SH-001' must be a string"));
    }

    [Fact]
    public void Implements_WithBaseTypes_Fails()
    {
        var yaml = RequireHead + "    require:\n      for: { types: [\"**.X\"] }\n      implements: { baseTypes: [\"**.B\"] }\n";

        Problems(yaml).Should().Contain(new PackProblem(8, 21, "unknown key 'baseTypes' in the implements of rule 'SH-001'"));
    }

    [Fact]
    public void ForbidTarget_AcceptsBaseTypesInterfacesAttributes()
    {
        var yaml = """
            schema: 2
            name: shop
            rules:
              - id: SH-001
                because: x
                forbid:
                  from: { baseTypes: ["**.Base"] }
                  to: { interfaces: ["**.IDisposable"], attributes: ["**.Obsolete*"] }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var rule = result.Value.Rules.First();
        rule.From.BaseTypes.Should().Equal("**.Base");
        rule.To.Interfaces.Should().Equal("**.IDisposable");
        rule.To.Attributes.Should().Equal("**.Obsolete*");
    }

    [Fact]
    public void ForTarget_MixingLayersWithInterfaces_Fails()
    {
        var yaml = RequireHead + "    require:\n      for: { layers: [Domain], interfaces: [\"**.IX\"] }\n      layer: Domain\n";

        var problem = Problems(yaml).Should().ContainSingle().Subject;
        problem.Line.Should().Be(7);
        problem.Message.Should().Contain("cannot mix 'layers'");
    }

    [Fact]
    public void BannedPackages_Valid_ReadsPackagesAndAssemblyScope()
    {
        var yaml = RequireHead + """
                bannedPackages:
                  packages: ["FluentValidation", "Microsoft.EntityFrameworkCore*"]
                  from: { assemblies: ["Shop.Domain", "Shop.Application"] }
                exceptions:
                  - assemblies: ["Shop.Legacy"]
                    because: Removed next release.
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var rule = result.Value.Rules.Should().ContainSingle().Subject;
        rule.Kind.Should().Be(RuleKind.BannedPackages);
        rule.Packages.Should().Equal("FluentValidation", "Microsoft.EntityFrameworkCore*");
        rule.From.Assemblies.Should().Equal("Shop.Domain", "Shop.Application");
        rule.Exceptions.Should().ContainSingle().Which.Assemblies.Should().Equal("Shop.Legacy");
    }

    [Fact]
    public void BannedPackages_NoFrom_CoversEveryProject()
    {
        var yaml = RequireHead + "    bannedPackages:\n      packages: [FluentValidation]\n";

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var rule = result.Value.Rules.Should().ContainSingle().Subject;
        rule.From.Assemblies.Should().BeEmpty();
        rule.From.Layers.Should().BeEmpty();
        rule.From.Namespaces.Should().BeEmpty();
    }

    [Fact]
    public void BannedPackages_EmptyPackages_FailsWithLineAndColumn()
    {
        var yaml = RequireHead + "    bannedPackages:\n      packages: []\n";

        Problems(yaml).Should().Equal(new PackProblem(7, 7, "the bannedPackages of rule 'SH-001' needs at least one entry in 'packages'"));
    }

    [Fact]
    public void BannedPackages_WithoutPackages_Fails()
    {
        var yaml = RequireHead + "    bannedPackages:\n      from: { assemblies: [Shop.Domain] }\n";

        Problems(yaml).Should().Equal(new PackProblem(7, 7, "the bannedPackages of rule 'SH-001' needs at least one entry in 'packages'"));
    }

    [Fact]
    public void BannedPackages_FromWithNamespaces_FailsBecausePackageBelongsToProject()
    {
        var yaml = RequireHead + "    bannedPackages:\n      packages: [FluentValidation]\n      from: { namespaces: [\"Shop.**\"] }\n";

        Problems(yaml).Should().Equal(
            new PackProblem(8, 15, "'namespaces' is not accepted in the from of rule 'SH-001'; a package belongs to a project, so 'from' selects projects by 'assemblies' only"));
    }

    [Fact]
    public void BannedPackages_ExceptionWithTypes_Fails()
    {
        var yaml = RequireHead + """
                bannedPackages:
                  packages: [FluentValidation]
                exceptions:
                  - types: ["**.Legacy"]
                    because: Old.
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(9, 9, "'types' is not accepted in exceptions[0] of rule 'SH-001'; a package belongs to a project, so the exception of a bannedPackages rule names it by 'assemblies'"));
    }

    [Fact]
    public void UniqueNames_Valid_ReadsForAndAgainst()
    {
        var yaml = RequireHead + """
                uniqueNames:
                  for: { layers: [Web] }
                  against: { namespaces: ["**.Dtos"], types: ["**.*Dto"] }
                exceptions:
                  - types: ["**.Legacy"]
                    because: Old.
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var rule = result.Value.Rules.Should().ContainSingle().Subject;
        rule.Kind.Should().Be(RuleKind.UniqueNames);
        rule.From.Layers.Should().Equal("Web");
        rule.Against.Namespaces.Should().Equal("**.Dtos");
        rule.Against.Types.Should().Equal("**.*Dto");
        rule.Exceptions.Should().ContainSingle().Which.Types.Should().Equal("**.Legacy");
    }

    [Fact]
    public void UniqueNames_EmptyFor_Fails()
    {
        var yaml = RequireHead + "    uniqueNames:\n      for: {}\n      against: { layers: [Application] }\n";

        Problems(yaml).Should().Equal(
            new PackProblem(7, 12, "the for of rule 'SH-001' needs at least one of 'layers', 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes', 'attributeArguments', 'onlyAttributes', 'dependsOn', 'onlyDependsOn'"));
    }

    [Fact]
    public void UniqueNames_WithoutAgainst_Fails()
    {
        var yaml = RequireHead + "    uniqueNames:\n      for: { layers: [Web] }\n";

        Problems(yaml).Should().Equal(new PackProblem(7, 7, "the uniqueNames of rule 'SH-001' has no 'against'"));
    }

    [Fact]
    public void UniqueNames_ExceptionWithAssemblies_Fails()
    {
        var yaml = RequireHead + """
                uniqueNames:
                  for: { layers: [Web] }
                  against: { layers: [Application] }
                exceptions:
                  - assemblies: ["Shop.Web"]
                    because: Old.
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(10, 9, "'assemblies' is not accepted in exceptions[0] of rule 'SH-001'; an exception of a uniqueNames rule excuses a type's name, so it takes 'namespaces', 'types', 'attributes', 'attributeArguments', 'onlyAttributes', a type trait, 'dependsOn' or 'onlyDependsOn'"));
    }

    [Fact]
    public void Members_InToSelector_Reads()
    {
        var yaml = RequireHead + """
                forbid:
                  from: { layers: [Domain] }
                  to: { members: ["System.Diagnostics.Process.Start", "System.Data.Common.DbCommand.Execute*"] }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var rule = result.Value.Rules.Should().ContainSingle().Subject;
        rule.To.Members.Should().Equal("System.Diagnostics.Process.Start", "System.Data.Common.DbCommand.Execute*");
        rule.From.Members.Should().BeEmpty();
    }

    [Fact]
    public void Members_InToSelector_CombinesWithNamespaces()
    {
        var yaml = RequireHead + """
                allowOnly:
                  from: { layers: [Infrastructure] }
                  to: { namespaces: ["Microsoft.Data.**"], members: ["**.SqlCommand.*"] }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var rule = result.Value.Rules.Should().ContainSingle().Subject;
        rule.Kind.Should().Be(RuleKind.AllowOnly);
        rule.To.Namespaces.Should().Equal("Microsoft.Data.**");
        rule.To.Members.Should().Equal("**.SqlCommand.*");
    }

    [Fact]
    public void Members_SingleSegmentPattern_FailsInPackTerms()
    {
        // A member name always has a type segment, so a one-segment pattern can never match; '**' alone still matches every member.
        var yaml = RequireHead + """
                forbid:
                  from: { layers: [Domain] }
                  to: { members: ["Start", "**", "*.Start", "*"] }
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(8, 23, "'Start' in the to of rule 'SH-001' is not a valid pattern: a member pattern needs a type and a member, as 'Namespace.Type.Member'; start it with '**.' to match the member in any type"),
            new PackProblem(8, 49, "'*' in the to of rule 'SH-001' is not a valid pattern: a member pattern needs a type and a member, as 'Namespace.Type.Member'; start it with '**.' to match the member in any type"));
    }

    [Fact]
    public void Members_InFromSelector_FailsInPackTerms()
    {
        var yaml = RequireHead + """
                forbid:
                  from: { namespaces: ["Shop.**"], members: ["System.Console.WriteLine"] }
                  to: { namespaces: ["System.Data.**"] }
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(7, 40, "'members' is not accepted in the from of rule 'SH-001'; it names APIs and only the 'to' of a 'forbid' or 'allowOnly' rule takes it"));
    }

    [Fact]
    public void Members_InRequireFor_Fails()
    {
        var yaml = RequireHead + "    require:\n      for: { members: [\"System.Console.WriteLine\"] }\n      layer: Domain\n";

        Problems(yaml).Should().Contain(p => p.Line == 7 && p.Message.StartsWith("'members' is not accepted in the for of rule 'SH-001'", StringComparison.Ordinal));
    }

    [Fact]
    public void Members_OnLayerEntry_Fails()
    {
        var yaml = """
            schema: 2
            name: shop
            layers:
              - name: Domain
                id: SH-DOMAIN
                because: x
                namespaces: ["**.Domain.**"]
                members: ["System.Console.WriteLine"]
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(8, 5, "'members' is not accepted in layer 'Domain'; it names APIs and only the 'to' of a 'forbid' or 'allowOnly' rule takes it"));
    }

    [Fact]
    public void Members_MixedWithLayers_FailsLikeOtherKeys()
    {
        var yaml = RequireHead + """
                forbid:
                  from: { layers: [Domain] }
                  to:
                    layers: [Infrastructure]
                    members: ["System.Console.WriteLine"]
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(9, 9, "a target in rule 'SH-001' cannot mix 'layers' with 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes', 'attributeArguments', 'onlyAttributes' or 'members'"));
    }

    [Fact]
    public void RuleBody_TwoKinds_FailsListingFiveKeys()
    {
        var yaml = RequireHead + """
                bannedPackages:
                  packages: [FluentValidation]
                uniqueNames:
                  for: { layers: [Web] }
                  against: { layers: [Application] }
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(8, 5, "rule 'SH-001' must have exactly one of 'forbid', 'allowOnly', 'require', 'bannedPackages', 'uniqueNames' or 'slices'"));
    }

    [Fact]
    public void BannedPackages_FromWithLayers_Fails()
    {
        var yaml = RequireHead + "    bannedPackages:\n      packages: [FluentValidation]\n      from: { layers: [Domain] }\n";

        Problems(yaml).Should().Equal(
            new PackProblem(8, 15, "'layers' is not accepted in the from of rule 'SH-001'; a package belongs to a project, so 'from' selects projects by 'assemblies' only"));
    }

    [Fact]
    public void BannedPackages_ExceptionWithNamespaces_Fails()
    {
        var yaml = RequireHead + """
                bannedPackages:
                  packages: [FluentValidation]
                exceptions:
                  - namespaces: ["Shop.Legacy"]
                    because: Old.
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(9, 9, "'namespaces' is not accepted in exceptions[0] of rule 'SH-001'; a package belongs to a project, so the exception of a bannedPackages rule names it by 'assemblies'"));
    }

    [Fact]
    public void BannedPackages_ExceptionWithOnlyBecause_NamesOnlyAssemblies()
    {
        var yaml = RequireHead + "    bannedPackages:\n      packages: [FluentValidation]\n    exceptions:\n      - because: Old.\n";

        Problems(yaml).Should().Equal(new PackProblem(9, 9, "exceptions[0] of rule 'SH-001' needs 'assemblies'"));
    }

    [Fact]
    public void UniqueNames_EmptyAgainst_Fails()
    {
        var yaml = RequireHead + "    uniqueNames:\n      for: { layers: [Web] }\n      against: {}\n";

        Problems(yaml).Should().Equal(
            new PackProblem(8, 16, "the against of rule 'SH-001' needs at least one of 'layers', 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes', 'attributeArguments', 'onlyAttributes', 'dependsOn', 'onlyDependsOn'"));
    }

    [Fact]
    public void UniqueNames_ExceptionWithNamespaces_Reads()
    {
        var yaml = RequireHead + """
                uniqueNames:
                  for: { layers: [Web] }
                  against: { layers: [Application] }
                exceptions:
                  - namespaces: ["Shop.Web.Legacy"]
                    because: Old.
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules.Should().ContainSingle().Which.Exceptions.Should().ContainSingle().Which.Namespaces.Should().Equal("Shop.Web.Legacy");
    }

    [Fact]
    public void UniqueNames_ExceptionWithOnlyBecause_NamesNamespacesAndTypes()
    {
        var yaml = RequireHead + "    uniqueNames:\n      for: { layers: [Web] }\n      against: { layers: [Application] }\n    exceptions:\n      - because: Old.\n";

        Problems(yaml).Should().Equal(new PackProblem(10, 9, "exceptions[0] of rule 'SH-001' needs at least one of 'namespaces', 'types', 'attributes', 'attributeArguments', 'onlyAttributes', 'names', 'kinds', 'accessibility', 'assignableTo', 'sealed', 'abstract', 'static', 'nested', 'dependsOn', 'onlyDependsOn'"));
    }

    [Fact]
    public void Layers_MixedWithTypes_NamesTheOtherKeys()
    {
        var yaml = RequireHead + "    require:\n      for: { layers: [Domain], types: [\"**.X\"] }\n      layer: Domain\n";

        Problems(yaml).Should().ContainSingle().Which.Message.Should().Contain("cannot mix 'layers' with 'namespaces'");
    }

    [Fact]
    public void Exception_with_attributes_reads()
    {
        var yaml = RequireHead + """
                forbid:
                  from: { namespaces: ["Shop.**"] }
                  to: { namespaces: ["Shop.Persistence.**"] }
                exceptions:
                  - attributes: ["Shop.**.AuditedAttribute"]
                    because: Audited types may use persistence.
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules.Should().ContainSingle().Which.Exceptions.Should().ContainSingle()
            .Which.Attributes.Should().Equal("Shop.**.AuditedAttribute");
    }

    [Fact]
    public void BannedPackages_ExceptionWithAttributes_Fails()
    {
        var yaml = RequireHead + """
                bannedPackages:
                  packages: [FluentValidation]
                exceptions:
                  - attributes: ["**.LegacyAttribute"]
                    because: Old.
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(9, 9, "'attributes' is not accepted in exceptions[0] of rule 'SH-001'; a package belongs to a project, so the exception of a bannedPackages rule names it by 'assemblies'"));
    }

    [Fact]
    public void Exception_attributes_with_bad_glob_fails()
    {
        var yaml = RequireHead + """
                forbid:
                  from: { namespaces: ["Shop.**"] }
                  to: { namespaces: ["Shop.Persistence.**"] }
                exceptions:
                  - attributes: ["A..B"]
                    because: x
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(10, 22, "'A..B' in exceptions[0] of rule 'SH-001' is not a valid pattern: pattern 'A..B' has an empty segment"));
    }

    private const string TraitNames =
        "'names', 'kinds', 'accessibility', 'assignableTo', 'sealed', 'abstract', 'static', 'nested'";

    [Fact]
    public void Reads_every_trait()
    {
        var yaml = RequireHead + """
                require:
                  for: { names: ["*Service"], kinds: [class, enum], accessibility: [public, "protected internal"], assignableTo: ["**.Base"], sealed: true, abstract: false, static: false, nested: true }
                  layer: Domain
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var subject = result.Value.Rules.Should().ContainSingle().Subject.From;
        subject.Names.Should().Equal("*Service");
        subject.Kinds.Should().Equal(CSharpTypeKind.Class, CSharpTypeKind.Enum);
        subject.Accessibility.Should().Equal("public", "protected internal");
        subject.AssignableTo.Should().Equal("**.Base");
        subject.Sealed.Should().BeTrue();
        subject.Abstract.Should().BeFalse();
        subject.Static.Should().BeFalse();
        subject.Nested.Should().BeTrue();
    }

    [Fact]
    public void Reads_every_kind_in_kinds()
    {
        var yaml = RequireHead + """
                require:
                  for: { kinds: [class, interface, record, struct, recordStruct, enum, delegate] }
                  layer: Domain
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules.Should().ContainSingle().Which.From.Kinds.Should().Equal(
            CSharpTypeKind.Class, CSharpTypeKind.Interface, CSharpTypeKind.Record, CSharpTypeKind.Struct,
            CSharpTypeKind.RecordStruct, CSharpTypeKind.Enum, CSharpTypeKind.Delegate);
    }

    [Fact]
    public void Reads_traits_in_from_and_to()
    {
        var yaml = RequireHead + """
                forbid:
                  from: { namespaces: ["Shop.**"], static: true }
                  to: { types: ["**.Clock"], kinds: [struct] }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var rule = result.Value.Rules.Should().ContainSingle().Subject;
        rule.From.Static.Should().BeTrue();
        rule.To.Kinds.Should().Equal(CSharpTypeKind.Struct);
    }

    [Fact]
    public void Trait_only_selector_loads()
    {
        var yaml = RequireHead + "    require:\n      for: { sealed: false }\n      layer: Domain\n";

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules.Should().ContainSingle().Which.From.Sealed.Should().BeFalse();
    }

    [Fact]
    public void Reads_exception_traits()
    {
        var yaml = """
            schema: 2
            name: shop
            extends: [builtin://clean-architecture]
            rules:
              - id: SH-001
                because: x
                forbid:
                  from: { namespaces: ["Shop.**"] }
                  to: { namespaces: ["System.IO.**"] }
                exceptions:
                  - assignableTo: ["**.Base"]
                    sealed: true
                    because: Bases may.
                  - kinds: [enum]
                    because: Enums may.
              - id: CA-001
                exceptions:
                  - assignableTo: ["**.Base"]
                    sealed: true
                    because: Bases may.
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var exceptions = result.Value.Rules.Should().ContainSingle().Subject.Exceptions;
        exceptions.Should().HaveCount(2);
        exceptions[0].AssignableTo.Should().Equal("**.Base");
        exceptions[0].Sealed.Should().BeTrue();
        exceptions[0].Because.Should().Be("Bases may.");
        exceptions[1].Kinds.Should().Equal(CSharpTypeKind.Enum);
        var amended = result.Value.RuleAmendments.Should().ContainSingle().Subject.Exceptions.Should().ContainSingle().Subject;
        amended.AssignableTo.Should().Equal("**.Base");
        amended.Sealed.Should().BeTrue();
        amended.Because.Should().Be("Bases may.");
    }

    [Fact]
    public void Reads_every_trait_in_an_exception()
    {
        var yaml = RequireHead + """
                forbid:
                  from: { namespaces: ["Shop.**"] }
                  to: { namespaces: ["System.IO.**"] }
                exceptions:
                  - names: ["*Io"]
                    kinds: [class]
                    accessibility: [internal]
                    assignableTo: ["**.Base"]
                    sealed: false
                    abstract: true
                    static: false
                    nested: false
                    because: x
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var exception = result.Value.Rules.Should().ContainSingle().Subject.Exceptions.Should().ContainSingle().Subject;
        exception.Names.Should().Equal("*Io");
        exception.Kinds.Should().Equal(CSharpTypeKind.Class);
        exception.Accessibility.Should().Equal("internal");
        exception.AssignableTo.Should().Equal("**.Base");
        exception.Sealed.Should().BeFalse();
        exception.Abstract.Should().BeTrue();
        exception.Static.Should().BeFalse();
        exception.Nested.Should().BeFalse();
    }

    [Fact]
    public void BannedPackages_exception_with_a_trait_fails()
    {
        var yaml = RequireHead + """
                bannedPackages:
                  packages: [FluentValidation]
                exceptions:
                  - sealed: true
                    because: Old.
            """;

        Problems(yaml).Should().Equal(
            new PackProblem(9, 9, "'sealed' is not accepted in exceptions[0] of rule 'SH-001'; a package belongs to a project, so the exception of a bannedPackages rule names it by 'assemblies'"));
    }

    [Fact]
    public void UniqueNames_exception_with_a_trait_reads()
    {
        var yaml = RequireHead + """
                uniqueNames:
                  for: { layers: [Web] }
                  against: { layers: [Application] }
                exceptions:
                  - nested: true
                    because: Nested types may repeat a name.
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules.Should().ContainSingle().Which.Exceptions.Should().ContainSingle().Which.Nested.Should().BeTrue();
    }

    [Fact]
    public void UniqueNames_target_with_a_trait_fails()
    {
        var yaml = RequireHead + "    uniqueNames:\n      for: { layers: [Web], sealed: true }\n      against: { layers: [Application] }\n";

        Problems(yaml).Should().ContainSingle().Which.Message.Should().StartWith("unknown key 'sealed' in the for of rule 'SH-001'");
    }

    [Fact]
    public void Reads_is_and_isNot()
    {
        var yaml = RequireHead + """
                require:
                  for: { namespaces: ["Shop.**"] }
                  is: { sealed: true, namespaces: ["Shop.Domain.**"], kinds: [class, record] }
                  isNot: { names: ["*Impl", "*Helper"], attributes: ["**.Obsolete*"] }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var requirement = result.Value.Rules.Should().ContainSingle().Subject.Require!;
        requirement.Is!.Sealed.Should().BeTrue();
        requirement.Is.Namespaces.Should().Equal("Shop.Domain.**");
        requirement.Is.Kinds.Should().Equal(CSharpTypeKind.Class, CSharpTypeKind.Record);
        requirement.IsNot!.Names.Should().Equal("*Impl", "*Helper");
        requirement.IsNot.Attributes.Should().Equal("**.Obsolete*");
        requirement.Layer.Should().BeNull();
    }

    [Fact]
    public void Require_with_only_is_loads()
    {
        var yaml = RequireHead + "    require:\n      for: { namespaces: [\"Shop.**\"] }\n      is: { abstract: true }\n";

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules.Should().ContainSingle().Which.Require!.Is!.Abstract.Should().BeTrue();
    }

    [Fact]
    public void Require_with_only_isNot_loads()
    {
        var yaml = RequireHead + "    allowEmpty: true\n    require:\n      for: { namespaces: [\"Shop.**\"] }\n      isNot: { types: [\"**\"] }\n";

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules.Should().ContainSingle().Which.Require!.IsNot!.Types.Should().Equal("**");
    }

    [Fact]
    public void Is_must_be_a_mapping()
    {
        var yaml = RequireHead + "    require:\n      for: { namespaces: [\"Shop.**\"] }\n      is: sealed\n";

        Problems(yaml).Should().Equal(new PackProblem(8, 7, "'is' in the require of rule 'SH-001' must be a mapping"));
    }

    [Fact]
    public void Empty_is_names_every_condition_key()
    {
        var yaml = RequireHead + "    require:\n      for: { namespaces: [\"Shop.**\"] }\n      is: {}\n";

        Problems(yaml).Should().Equal(new PackProblem(8, 11,
            "the is of rule 'SH-001' needs at least one of 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes', 'attributeArguments', 'onlyAttributes', " + TraitNames + ", " + DependencyNames));
    }

    [Fact]
    public void Layers_with_traits_loads()
    {
        var yaml = RequireHead + "    require:\n      for: { layers: [Domain], sealed: true, names: [\"*Service\"] }\n      layer: Domain\n";

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var subject = result.Value.Rules.Should().ContainSingle().Subject.From;
        subject.Layers.Should().Equal("Domain");
        subject.Sealed.Should().BeTrue();
        subject.Names.Should().Equal("*Service");
    }

    [Fact]
    public void Layers_with_namespaces_still_fails()
    {
        var yaml = RequireHead + "    require:\n      for: { layers: [Domain], sealed: true, namespaces: [\"Shop.**\"] }\n      layer: Domain\n";

        Problems(yaml).Should().Equal(new PackProblem(7, 14,
            "a target in rule 'SH-001' cannot mix 'layers' with 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes', 'attributeArguments' or 'onlyAttributes'"));
    }

    [Theory]
    [InlineData("kinds: [enumeration]", 17,
        "kind 'enumeration' in the for of rule 'SH-001' must be one of 'class', 'interface', 'record', 'struct', 'recordStruct', 'enum', 'delegate'")]
    [InlineData("kinds: [Class]", 17,
        "kind 'Class' in the for of rule 'SH-001' must be one of 'class', 'interface', 'record', 'struct', 'recordStruct', 'enum', 'delegate'")]
    [InlineData("accessibility: [friend]", 25,
        "accessibility 'friend' in the for of rule 'SH-001' must be one of 'public', 'internal', 'protected', 'private', 'protected internal', 'private protected'")]
    [InlineData("sealed: yes", 9, "'sealed' in the for of rule 'SH-001' must be true or false")]
    [InlineData("abstract: \"true\"", 9, "'abstract' in the for of rule 'SH-001' must be true or false")]
    [InlineData("static: [true]", 9, "'static' in the for of rule 'SH-001' must be true or false")]
    [InlineData("nested: 1", 9, "'nested' in the for of rule 'SH-001' must be true or false")]
    [InlineData("names: [\"a.b\"]", 17,
        "'a.b' in the for of rule 'SH-001' is not a valid pattern: a name pattern matches a type's simple name, so it cannot contain '.'")]
    [InlineData("assignableTo: [\"a..b\"]", 24, "'a..b' in the for of rule 'SH-001' is not a valid pattern: pattern 'a..b' has an empty segment")]
    [InlineData("kinds: class", 9, "'kinds' in the for of rule 'SH-001' must be a list of strings")]
    public void Rejects_bad_trait_values(string entry, int column, string message)
    {
        var yaml = RequireHead + $"    require:\n      for:\n        types: [\"**.X\"]\n        {entry}\n      layer: Domain\n";

        Problems(yaml).Should().Equal(new PackProblem(9, column, message));
    }

    [Fact]
    public void Rejects_layers_in_is()
    {
        var yaml = RequireHead + "    require:\n      for: { namespaces: [\"Shop.**\"] }\n      is: { layers: [L], sealed: true }\n";

        Problems(yaml).Should().Equal(new PackProblem(8, 13, "unknown key 'layers' in the is of rule 'SH-001'"));
    }

    [Fact]
    public void Rejects_members_in_isNot()
    {
        var yaml = RequireHead + "    require:\n      for: { namespaces: [\"Shop.**\"] }\n      isNot: { members: [\"**.M\"], sealed: true }\n";

        Problems(yaml).Should().Equal(new PackProblem(8, 16,
            "'members' is not accepted in the isNot of rule 'SH-001'; it names APIs and only the 'to' of a 'forbid' or 'allowOnly' rule takes it"));
    }

    [Fact]
    public void Kind_enum_keeps_the_five_values()
    {
        var yaml = RequireHead + "    require:\n      for: { types: [\"**.X\"] }\n      kind: enum\n";

        Problems(yaml).Should().Equal(
            new PackProblem(8, 7, "kind 'enum' in the require of rule 'SH-001' must be one of Class, Interface, Record, Struct, RecordStruct"));
    }

    [Fact]
    public void Role_typeKind_delegate_keeps_the_five_values()
    {
        var yaml = "schema: 2\nname: shop\ncomponentRoles:\n  - name: Handler\n    layer: Application\n    typeKind: Delegate\n";

        Problems(yaml).Should().Equal(
            new PackProblem(6, 5, "typeKind 'Delegate' in role 'Handler' must be one of Class, Interface, Record, Struct, RecordStruct"));
    }

    [Fact]
    public void Require_needs_a_condition()
    {
        var yaml = RequireHead + "    require:\n      for: { types: [\"**.X\"] }\n";

        Problems(yaml).Should().Equal(
            new PackProblem(7, 7, "the require of rule 'SH-001' needs at least one of 'layer', 'kind', 'accessibility', 'implements', 'is', 'isNot'"));
    }

    [Theory]
    [InlineData("names: []")]
    [InlineData("kinds: []")]
    [InlineData("accessibility: []")]
    [InlineData("assignableTo: []")]
    public void Empty_trait_list_is_an_error(string entry)
    {
        var yaml = RequireHead + $"    require:\n      for: {{ {entry} }}\n      layer: Domain\n";

        Problems(yaml).Should().Equal(new PackProblem(7, 12,
            "the for of rule 'SH-001' needs at least one of 'layers', 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes', 'attributeArguments', 'onlyAttributes', " + TraitNames + ", " + DependencyNames));
    }

    [Fact]
    public void Exception_with_only_because_names_the_traits()
    {
        var yaml = RequireHead + """
                forbid:
                  from: { namespaces: ["Shop.**"] }
                  to: { namespaces: ["System.IO.**"] }
                exceptions:
                  - because: x
            """;

        Problems(yaml).Should().Equal(new PackProblem(10, 9,
            "exceptions[0] of rule 'SH-001' needs at least one of 'namespaces', 'assemblies', 'types', 'attributes', 'attributeArguments', 'onlyAttributes', " + TraitNames + ", " + DependencyNames));
    }

    [Fact]
    public void AttributeArguments_in_forbid_from_reads()
    {
        var yaml = RequireHead + """
                forbid:
                  from:
                    attributeArguments:
                      - attribute: "Shop.RouteAttribute"
                        arguments: ["api/orders", 2]
                        named: { Version: 2 }
                  to: { namespaces: ["Lib.**"] }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var entry = result.Value.Rules.Should().ContainSingle().Subject.From.AttributeArguments.Should().ContainSingle().Subject;
        entry.Attribute.Should().Be("Shop.RouteAttribute");
        entry.Arguments.Should().Equal("api/orders", "2");
        entry.Named.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>("Version", "2"));
        entry.Named.ContainsKey("version").Should().BeFalse("named argument names compare ordinal");
    }

    [Fact]
    public void AttributeArguments_in_is_reads()
    {
        var yaml = RequireHead + """
                require:
                  for: { namespaces: ["Shop.**"] }
                  is:
                    attributeArguments:
                      - arguments: [Singleton]
                      - attribute: "**.RouteAttribute"
                        named: { Name: orders }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var entries = result.Value.Rules.Should().ContainSingle().Subject.Require!.Is!.AttributeArguments;
        entries.Should().HaveCount(2);
        entries[0].Attribute.Should().BeNull();
        entries[0].Arguments.Should().Equal("Singleton");
        entries[0].Named.Should().BeEmpty();
        entries[1].Attribute.Should().Be("**.RouteAttribute");
        entries[1].Arguments.Should().BeEmpty();
        entries[1].Named.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>("Name", "orders"));
    }

    [Fact]
    public void OnlyAttributes_in_isNot_reads()
    {
        var yaml = RequireHead + """
                require:
                  for: { namespaces: ["Shop.**"] }
                  isNot: { onlyAttributes: ["System.**", "Shop.**.AuditedAttribute"] }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules.Should().ContainSingle().Subject.Require!.IsNot!.OnlyAttributes
            .Should().Equal("System.**", "Shop.**.AuditedAttribute");
    }

    [Fact]
    public void Exception_with_only_attribute_arguments_reads()
    {
        var yaml = RequireHead + """
                forbid:
                  from: { namespaces: ["Shop.**"] }
                  to: { namespaces: ["Shop.Persistence.**"] }
                exceptions:
                  - attributeArguments:
                      - named: { Legacy: true }
                    because: Legacy types may use persistence.
                  - onlyAttributes: ["System.**"]
                    because: Plain types may too.
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var exceptions = result.Value.Rules.Should().ContainSingle().Subject.Exceptions;
        exceptions.Should().HaveCount(2);
        exceptions[0].AttributeArguments.Should().ContainSingle().Which.Named.Should()
            .ContainSingle().Which.Should().Be(new KeyValuePair<string, string>("Legacy", "true"));
        exceptions[1].OnlyAttributes.Should().Equal("System.**");
    }

    [Fact]
    public void OnlyAttributes_in_uniqueNames_for_reads()
    {
        var yaml = RequireHead + """
                uniqueNames:
                  for: { onlyAttributes: ["Shop.**"] }
                  against: { attributeArguments: [{ arguments: [dto] }] }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var rule = result.Value.Rules.Should().ContainSingle().Subject;
        rule.From.OnlyAttributes.Should().Equal("Shop.**");
        rule.Against.AttributeArguments.Should().ContainSingle().Which.Arguments.Should().Equal("dto");
    }

    // A forbid whose from starts on line 9 at column 9; the entry of attributeArguments starts on line 10 at column 13.
    private const string AttributeArgumentsHead =
        RequireHead + "    forbid:\n      to: { namespaces: [\"Lib.**\"] }\n      from:\n";

    private const string EntryWhere = "attributeArguments[0] of the from of rule 'SH-001'";

    [Theory]
    [InlineData("        attributeArguments:\n          - attribute: \"Shop.RouteAttribute\"\n", 10, 13,
        EntryWhere + " needs 'arguments' or 'named'; to select by the attribute alone, use 'attributes'")]
    [InlineData("        attributeArguments:\n          - arguments: []\n", 10, 13,
        "'arguments' in " + EntryWhere + " needs at least one value")]
    [InlineData("        onlyAttributes: []\n", 9, 9,
        "'onlyAttributes' in the from of rule 'SH-001' needs at least one pattern; to select types without attributes, use isNot: { attributes: [\"**\"] }")]
    [InlineData("        attributeArguments:\n          - named: { Version: null }\n", 10, 31,
        "the value of 'Version' in " + EntryWhere + " is null; quote it to match the text 'null'")]
    [InlineData("        attributeArguments:\n          - arguments: [~]\n", 10, 25,
        "a value in 'arguments' of " + EntryWhere + " is null; quote it to match the text 'null'")]
    [InlineData("        attributeArguments:\n          - arguments: [[1, 2]]\n", 10, 25,
        "a value in 'arguments' of " + EntryWhere + " must be a single value, not a list or mapping")]
    [InlineData("        attributeArguments:\n          - named: { Version: { Major: 2 } }\n", 10, 31,
        "the value of 'Version' in " + EntryWhere + " must be a single value, not a list or mapping")]
    [InlineData("        attributeArguments:\n          - named: { Version: 1, Version: 2 }\n", 10, 34,
        "duplicate key 'Version' in " + EntryWhere)]
    [InlineData("        attributeArguments:\n          - named: {}\n", 10, 13,
        "'named' in " + EntryWhere + " needs at least one name")]
    [InlineData("        attributeArguments:\n          - named: { \" \": 1 }\n", 10, 22,
        "a name in 'named' of " + EntryWhere + " must be a string")]
    [InlineData("        attributeArguments:\n          - named: { ~: 1 }\n", 10, 22,
        "a name in 'named' of " + EntryWhere + " must be a string")]
    [InlineData("        attributeArguments:\n          - arguments: [1]\n            type: \"X\"\n", 11, 13,
        "unknown key 'type' in " + EntryWhere)]
    [InlineData("        attributeArguments:\n          - attribute: \"A..B\"\n            arguments: [1]\n", 10, 24,
        "'A..B' in " + EntryWhere + " is not a valid pattern: pattern 'A..B' has an empty segment")]
    [InlineData("        attributeArguments: [\"Shop.RouteAttribute\"]\n", 9, 30,
        "'attributeArguments[0]' in the from of rule 'SH-001' must be a mapping")]
    [InlineData("        attributeArguments: { arguments: [1] }\n", 9, 9,
        "'attributeArguments' in the from of rule 'SH-001' must be a list")]
    [InlineData("        attributeArguments:\n          - arguments: 1\n", 10, 13,
        "'arguments' in " + EntryWhere + " must be a list")]
    [InlineData("        attributeArguments:\n          - named: [Version]\n", 10, 13,
        "'named' in " + EntryWhere + " must be a mapping")]
    public void Rejects_a_bad_attribute_argument_entry(string from, int line, int column, string message)
    {
        Problems(AttributeArgumentsHead + from).Should().Equal(new PackProblem(line, column, message));
    }

    [Theory]
    [InlineData("attributeArguments: [{ arguments: [1] }]")]
    [InlineData("onlyAttributes: [\"Shop.**\"]")]
    public void Attribute_argument_keys_cannot_mix_with_layers(string entry)
    {
        var yaml = RequireHead + $"    require:\n      for: {{ layers: [Domain], {entry} }}\n      layer: Domain\n";

        Problems(yaml).Should().Equal(new PackProblem(7, 14,
            "a target in rule 'SH-001' cannot mix 'layers' with 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes', 'attributeArguments' or 'onlyAttributes'"));
    }

    [Theory]
    [InlineData("attributeArguments: [{ arguments: [1] }]", "attributeArguments")]
    [InlineData("onlyAttributes: [\"Shop.**\"]", "onlyAttributes")]
    public void BannedPackages_exception_rejects_attribute_argument_keys(string entry, string key)
    {
        var yaml = RequireHead + $"    bannedPackages:\n      packages: [FluentValidation]\n    exceptions:\n      - {entry}\n        because: Old.\n";

        Problems(yaml).Should().Equal(new PackProblem(9, 9,
            $"'{key}' is not accepted in exceptions[0] of rule 'SH-001'; a package belongs to a project, so the exception of a bannedPackages rule names it by 'assemblies'"));
    }

    [Fact]
    public void Capture_in_an_attribute_argument_pattern_of_a_slices_rule_fails()
    {
        var yaml = RequireHead + """
                slices:
                  matching: ["Shop.(*).**"]
                  independent: true
                  allow:
                    attributeArguments:
                      - attribute: "Shop.(*).RouteAttribute"
                        arguments: [1]
            """;

        Problems(yaml).Should().Equal(new PackProblem(11, 24,
            "'Shop.(*).RouteAttribute' in attributeArguments[0] of the allow of rule 'SH-001' is not a valid pattern: a capture, (*) or (**), is only accepted in the 'matching' of a slices rule"));
    }

    private const string DependencyNames = "'dependsOn', 'onlyDependsOn'";

    [Fact]
    public void DependsOn_next_to_layers_in_forbid_from_reads()
    {
        var yaml = RequireHead + """
                forbid:
                  from: { layers: [Web], dependsOn: { layers: [Infrastructure] } }
                  to: { namespaces: ["Shop.Web.Pages.**"], onlyDependsOn: { namespaces: ["Shop.**"], sealed: true } }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var rule = result.Value.Rules.Should().ContainSingle().Subject;
        rule.From.Layers.Should().Equal("Web");
        rule.From.DependsOn!.Layers.Should().Equal("Infrastructure");
        rule.From.OnlyDependsOn.Should().BeNull();
        rule.To.OnlyDependsOn!.Namespaces.Should().Equal("Shop.**");
        rule.To.OnlyDependsOn.Sealed.Should().BeTrue();
    }

    [Fact]
    public void OnlyDependsOn_in_is_reads()
    {
        var yaml = RequireHead + """
                require:
                  for: { layers: [Domain], dependsOn: { interfaces: ["Shop.IClock"] } }
                  is: { onlyDependsOn: { layers: [Domain] } }
                  isNot: { dependsOn: { attributeArguments: [{ arguments: [legacy] }], onlyAttributes: ["Shop.**"] } }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var rule = result.Value.Rules.Should().ContainSingle().Subject;
        rule.From.DependsOn!.Interfaces.Should().Equal("Shop.IClock");
        rule.Require!.Is!.OnlyDependsOn!.Layers.Should().Equal("Domain");
        rule.Require.IsNot!.DependsOn!.AttributeArguments.Should().ContainSingle().Which.Arguments.Should().Equal("legacy");
        rule.Require.IsNot.DependsOn.OnlyAttributes.Should().Equal("Shop.**");
    }

    [Fact]
    public void DependsOn_in_exception_with_layers_inside_reads()
    {
        var yaml = RequireHead + """
                forbid:
                  from: { namespaces: ["Shop.**"] }
                  to: { namespaces: ["System.IO.**"] }
                exceptions:
                  - dependsOn: { layers: [Infrastructure] }
                    because: Adapters.
                  - onlyDependsOn: { layers: [Domain], kinds: [record] }
                    because: Pure.
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var exceptions = result.Value.Rules.Should().ContainSingle().Subject.Exceptions;
        exceptions[0].DependsOn!.Layers.Should().Equal("Infrastructure");
        exceptions[1].OnlyDependsOn!.Layers.Should().Equal("Domain");
        exceptions[1].OnlyDependsOn!.Kinds.Should().Equal(CSharpTypeKind.Record);
    }

    [Fact]
    public void DependsOn_in_uniqueNames_for_reads()
    {
        var yaml = RequireHead + """
                uniqueNames:
                  for: { namespaces: ["Shop.Web.**"], dependsOn: { layers: [Infrastructure] } }
                  against: { onlyDependsOn: { namespaces: ["Shop.Application.**"] } }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        var rule = result.Value.Rules.Should().ContainSingle().Subject;
        rule.From.DependsOn!.Layers.Should().Equal("Infrastructure");
        rule.Against.OnlyDependsOn!.Namespaces.Should().Equal("Shop.Application.**");
    }

    [Fact]
    public void DependsOn_in_slices_allow_reads()
    {
        var yaml = RequireHead + """
                slices:
                  matching: ["Shop.(*).**"]
                  independent: true
                  allow: { dependsOn: { namespaces: ["Shop.Shared.**"] } }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules.Should().ContainSingle().Subject.Slices!.Allow!.DependsOn!.Namespaces.Should().Equal("Shop.Shared.**");
    }

    private const string NestedWhere = "the dependsOn of the from of rule 'SH-001'";

    [Theory]
    [InlineData("        dependsOn: {}\n", 9, 20,
        NestedWhere + " needs at least one of 'layers', 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes', 'attributeArguments', 'onlyAttributes', 'names', 'kinds', 'accessibility', 'assignableTo', 'sealed', 'abstract', 'static', 'nested'")]
    [InlineData("        dependsOn: { layers: [A], dependsOn: { layers: [B] } }\n", 9, 35,
        "'dependsOn' is not accepted in " + NestedWhere + "; selectors nest one level only, so the selector under 'dependsOn' or 'onlyDependsOn' cannot hold either")]
    [InlineData("        dependsOn: { layers: [A], onlyDependsOn: { layers: [B] } }\n", 9, 35,
        "'onlyDependsOn' is not accepted in " + NestedWhere + "; selectors nest one level only, so the selector under 'dependsOn' or 'onlyDependsOn' cannot hold either")]
    [InlineData("        dependsOn: { attributeArguments: [{ attribute: X, arguments: [1], dependsOn: { layers: [B] } }] }\n", 9, 75,
        "unknown key 'dependsOn' in attributeArguments[0] of " + NestedWhere)]
    [InlineData("        dependsOn: { layers: [A], members: [\"**.M\"] }\n", 9, 35,
        "'members' is not accepted in " + NestedWhere + "; it names APIs and only the 'to' of a 'forbid' or 'allowOnly' rule takes it")]
    [InlineData("        dependsOn: { layers: [A], namespaces: [\"X.**\"] }\n", 9, 22,
        "a target in " + NestedWhere + " cannot mix 'layers' with 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes', 'attributeArguments' or 'onlyAttributes'")]
    [InlineData("        dependsOn: Shop.Infrastructure\n", 9, 9,
        "'dependsOn' in the from of rule 'SH-001' must be a mapping")]
    [InlineData("        onlyDependsOn: [\"Shop.**\"]\n", 9, 9,
        "'onlyDependsOn' in the from of rule 'SH-001' must be a mapping")]
    [InlineData("        dependsOn: { kinds: [enumeration] }\n", 9, 30,
        "kind 'enumeration' in " + NestedWhere + " must be one of 'class', 'interface', 'record', 'struct', 'recordStruct', 'enum', 'delegate'")]
    public void Rejects_a_bad_dependency_selector(string from, int line, int column, string message)
    {
        Problems(AttributeArgumentsHead + from).Should().Equal(new PackProblem(line, column, message));
    }

    [Theory]
    [InlineData("dependsOn")]
    [InlineData("onlyDependsOn")]
    public void BannedPackages_exception_rejects_dependency_keys(string key)
    {
        var yaml = RequireHead + $"    bannedPackages:\n      packages: [FluentValidation]\n    exceptions:\n      - {key}: {{ layers: [A] }}\n        because: Old.\n";

        Problems(yaml).Should().Equal(new PackProblem(9, 9,
            $"'{key}' is not accepted in exceptions[0] of rule 'SH-001'; a package belongs to a project, so the exception of a bannedPackages rule names it by 'assemblies'"));
    }

    [Theory]
    [InlineData("dependsOn")]
    [InlineData("onlyDependsOn")]
    public void Implements_rejects_dependency_keys(string key)
    {
        var yaml = RequireHead + $"    require:\n      for: {{ types: [\"**.X\"] }}\n      implements: {{ types: [\"**.I\"], {key}: {{ layers: [A] }} }}\n";

        Problems(yaml).Should().Equal(new PackProblem(8, 38, $"unknown key '{key}' in the implements of rule 'SH-001'"));
    }

    [Fact]
    public void Empty_from_names_the_dependency_keys()
    {
        var yaml = RequireHead + "    forbid:\n      from: {}\n      to: { namespaces: [\"Lib.**\"] }\n";

        Problems(yaml).Should().Equal(new PackProblem(7, 13,
            "the from of rule 'SH-001' needs at least one of 'layers', 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes', 'attributeArguments', 'onlyAttributes', "
            + TraitNames + ", " + DependencyNames));
    }

    [Fact]
    public void A_selector_with_only_a_dependency_key_is_not_empty()
    {
        var yaml = RequireHead + "    require:\n      for: { onlyDependsOn: { layers: [Domain] } }\n      isNot: { dependsOn: { layers: [Web] } }\n";

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
    }

    private const string RequireTargetMessage = "'requireTarget' applies only to forbid, allowOnly and uniqueNames rules";

    [Fact]
    public void Reads_requireTarget_on_forbid_allowOnly_and_uniqueNames()
    {
        var yaml = """
            schema: 2
            name: shop
            rules:
              - id: R-FORBID
                because: x
                forbid:
                  from: { namespaces: ["Shop.Domain.**"] }
                  to: { namespaces: ["Thalos.**"] }
                requireTarget: true
              - id: R-ALLOWONLY
                because: x
                allowOnly:
                  from: { namespaces: ["Shop.Agents.**"] }
                  to: { types: ["Thalos.ISubagentRunner"] }
                requireTarget: true
              - id: R-UNIQUE
                because: x
                uniqueNames:
                  for: { namespaces: ["Shop.Web.**"] }
                  against: { namespaces: ["Shop.Application.DTOs.**"] }
                requireTarget: true
              - id: R-PLAIN
                because: x
                forbid:
                  from: { namespaces: ["Shop.Domain.**"] }
                  to: { namespaces: ["Newtonsoft.**"] }
            """;

        var result = PackYamlReader.Read(yaml);

        result.IsSuccess.Should().BeTrue(because: Describe(result));
        result.Value.Rules.Select(r => r.RequireTarget).Should().Equal(true, true, true, false);
    }

    [Theory]
    [InlineData("    requireTarget: true\n    require:\n      for: { namespaces: [\"Shop.**\"] }\n      isNot: { types: [\"**\"] }\n")]
    [InlineData("    requireTarget: true\n    bannedPackages:\n      packages: [FluentValidation]\n")]
    [InlineData("    requireTarget: true\n    slices:\n      matching: [\"Shop.(*)\"]\n      freeOfCycles: true\n")]
    public void Rejects_requireTarget_elsewhere(string body)
    {
        Problems(RequireHead + body).Should().Equal(new PackProblem(6, 5, RequireTargetMessage));
    }

    [Fact]
    public void Rejects_requireTarget_elsewhere_with_the_location_in_the_loader_detail()
    {
        var yaml = RequireHead + "    requireTarget: true\n    bannedPackages:\n      packages: [FluentValidation]\n";

        var result = new YamlRulePackLoader().LoadFromString(yaml);

        result.IsFailure.Should().BeTrue();
        result.Error.Detail.Should().Be("rule pack YAML:6:5: " + RequireTargetMessage);
    }

    [Fact]
    public void Rejects_non_boolean_requireTarget()
    {
        var yaml = RequireHead + "    requireTarget: yes\n    forbid:\n      from: { namespaces: [\"Shop.**\"] }\n      to: { namespaces: [\"Lib.**\"] }\n";

        Problems(yaml).Should().Equal(new PackProblem(6, 5, "'requireTarget' in rule 'SH-001' must be true or false"));
    }

    private static IReadOnlyList<PackProblem> Problems(string yaml)
    {
        var result = PackYamlReader.Read(yaml);
        result.IsSuccess.Should().BeFalse("the pack has problems");
        return result.Error;
    }

    private static string Describe(ZeroAlloc.Results.Result<RulePack, IReadOnlyList<PackProblem>> result) =>
        result.IsSuccess ? "" : string.Join("; ", result.Error.Select(p => $"{p.Line}:{p.Column}: {p.Message}"));
}
