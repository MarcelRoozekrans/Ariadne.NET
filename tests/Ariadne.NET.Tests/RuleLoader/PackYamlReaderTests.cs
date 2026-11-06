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
            new PackProblem(9, 5, "rule 'SH-001' must have exactly one of 'forbid', 'allowOnly' or 'require'"));
    }

    [Fact]
    public void RuleWithNeitherForbidNorAllowOnly()
    {
        Problems("schema: 2\nname: shop\nrules:\n  - id: SH-001\n    because: x\n    locked: false\n").Should().Equal(
            new PackProblem(4, 5, "rule 'SH-001' must have exactly one of 'forbid', 'allowOnly' or 'require'"));
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
            new PackProblem(8, 9, "a target in rule 'SH-001' cannot mix 'layers' with 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces' or 'attributes'"));
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
            new PackProblem(15, 9, "exceptions[0] of rule 'SH-001' needs at least one of 'namespaces', 'assemblies', 'types'"));
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
            new PackProblem(8, 11, "the to of rule 'SH-001' needs at least one of 'layers', 'namespaces', 'assemblies', 'types', 'baseTypes', 'interfaces', 'attributes'"));
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
            new PackProblem(7, 7, "the require of rule 'SH-001' needs at least one of 'layer', 'kind', 'accessibility', 'implements'"));
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
            new PackProblem(9, 5, "rule 'SH-001' must have exactly one of 'forbid', 'allowOnly' or 'require'"));
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

    private static IReadOnlyList<PackProblem> Problems(string yaml)
    {
        var result = PackYamlReader.Read(yaml);
        result.IsSuccess.Should().BeFalse("the pack has problems");
        return result.Error;
    }

    private static string Describe(ZeroAlloc.Results.Result<RulePack, IReadOnlyList<PackProblem>> result) =>
        result.IsSuccess ? "" : string.Join("; ", result.Error.Select(p => $"{p.Line}:{p.Column}: {p.Message}"));
}
