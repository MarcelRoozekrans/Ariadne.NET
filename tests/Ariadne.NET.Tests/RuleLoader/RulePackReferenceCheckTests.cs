using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;
using FluentAssertions;

namespace Ariadne.NET.Tests.RuleLoader;

public class RulePackReferenceCheckTests
{
    // The check is internal to the engine; the resolver runs it on every resolve, including a pack with no extends.
    private static ZeroAlloc.Results.Result<RulePack, AriadneError> Resolve(RulePack pack) =>
        new RulePackResolver(
            new BuiltinPackRegistry(new YamlRulePackLoader()),
            new YamlRulePackLoader(),
            new Ariadne.NET.RuleEngine.RulePackMerger()).Resolve(pack);

    private static RulePack Pack(
        IList<LayerDefinition>? layers = null,
        IList<ComponentRoleDefinition>? roles = null) =>
        new()
        {
            Name = "p",
            Layers = layers ?? [Layer("Domain")],
            ComponentRoles = roles ?? [],
        };

    private static LayerDefinition Layer(string name, params string[] allowed) =>
        new() { Name = name, Id = "L-" + name, Because = name + ".", AllowedDependencies = [.. allowed] };

    [Fact]
    public void Consistent_pack_passes()
    {
        var pack = Pack(
            [Layer("Domain"), Layer("App", "domain")],
            [
                new ComponentRoleDefinition { Name = "Entity", Layer = "Domain" },
                new ComponentRoleDefinition { Name = "Service", Layer = "APP", RequiresInterface = true, InterfaceRole = "entity" },
            ]);

        Resolve(pack).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Unknown_allowed_dependency_is_invalid_and_named()
    {
        var pack = Pack([Layer("App", "Ghost")]);

        var result = Resolve(pack);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Rule pack 'p' is invalid.");
        result.Error.Detail.Should().Be("Layers[0].AllowedDependencies[0]: refers to unknown layer 'Ghost'.");
    }

    [Fact]
    public void Role_in_an_unknown_layer_is_invalid()
    {
        var pack = Pack(roles: [new ComponentRoleDefinition { Name = "Entity", Layer = "Ghost" }]);

        Resolve(pack).Error.Detail
            .Should().Be("ComponentRoles[0].Layer: refers to unknown layer 'Ghost'.");
    }

    [Fact]
    public void Pack_without_layers_may_name_any_role_layer()
    {
        var pack = Pack(layers: [], roles: [new ComponentRoleDefinition { Name = "Command", Layer = "Application" }]);

        Resolve(pack).IsSuccess.Should().BeTrue("role-only packs such as cqrs declare no layers");
    }

    [Fact]
    public void Unknown_interface_role_is_invalid()
    {
        var pack = Pack(roles: [new ComponentRoleDefinition { Name = "Service", Layer = "Domain", InterfaceRole = "Ghost" }]);

        Resolve(pack).Error.Detail
            .Should().Be("ComponentRoles[0].InterfaceRole: refers to unknown role 'Ghost'.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Absent_interface_role_passes(string? interfaceRole)
    {
        var pack = Pack(roles: [new ComponentRoleDefinition { Name = "Service", Layer = "Domain", InterfaceRole = interfaceRole }]);

        Resolve(pack).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Layer_names_differing_only_in_case_are_duplicates()
    {
        var pack = Pack([Layer("Domain"), new LayerDefinition { Name = "DOMAIN", Id = "L-DOMAIN-2", Because = "x" }]);

        Resolve(pack).Error.Detail
            .Should().Be("Layers[1].Name: duplicate layer name 'DOMAIN'.");
    }

    [Fact]
    public void Role_names_differing_only_in_case_are_duplicates()
    {
        var pack = Pack(roles:
        [
            new ComponentRoleDefinition { Name = "Entity", Layer = "Domain" },
            new ComponentRoleDefinition { Name = "entity", Layer = "Domain" },
        ]);

        Resolve(pack).Error.Detail
            .Should().Be("ComponentRoles[1].Name: duplicate role name 'entity'.");
    }

    [Fact]
    public void Every_failure_is_listed_on_its_own_line()
    {
        var pack = Pack(
            [Layer("App", "X", "Y")]);

        Resolve(pack).Error.Detail!.Split('\n').Should().HaveCount(2);
    }

    [Fact]
    public void DuplicateIdAcrossLayerAndRule_Fails()
    {
        var pack = new RulePack
        {
            Name = "p",
            Layers = [new LayerDefinition { Name = "Domain", Id = "X-1" }],
            Rules = [Rule("X-1", ["Domain"], ["Domain"])],
        };

        Resolve(pack).Error.Detail.Should().Be("duplicate id 'X-1'");
    }

    [Fact]
    public void EmptyIds_AreEachReported_NotAsDuplicates()
    {
        var pack = new RulePack
        {
            Name = "p",
            Layers = [new LayerDefinition { Name = "Domain" }, new LayerDefinition { Name = "App" }],
            Rules = [Rule("", ["Domain"], ["App"])],
        };

        Resolve(pack).Error.Detail!.Split('\n').Should().Equal(
            "Layers[0].Id: Id must not be empty.",
            "Layers[1].Id: Id must not be empty.",
            "Rules[0].Id: Id must not be empty.");
    }

    [Fact]
    public void PackTextInMessages_IsEscapedAndCut()
    {
        var pack = new RulePack
        {
            Name = "p",
            Layers = [Layer("Domain")],
            Rules = [Rule("R\u001b[2J", ["Domain"], [new string('g', 100)])],
        };

        Resolve(pack).Error.Detail.Should().Be(
            $"rule 'R\\u001b[2J' refers to unknown layer '{new string('g', 80)}...'");
    }

    [Fact]
    public void RuleUnknownLayer_Fails()
    {
        var pack = new RulePack
        {
            Name = "p",
            Layers = [new LayerDefinition { Name = "Domain", Id = "L-1" }],
            Rules = [Rule("R-1", ["domain", "Ghost"], ["Nope"])],
        };

        Resolve(pack).Error.Detail!.Split('\n').Should().Equal(
            "rule 'R-1' refers to unknown layer 'Ghost'",
            "rule 'R-1' refers to unknown layer 'Nope'");
    }

    [Fact]
    public void RoleOnlyPack_LoadsAlone()
    {
        var pack = new RulePack
        {
            Name = "cqrs",
            ComponentRoles = [new ComponentRoleDefinition { Name = "Command", Layer = "Application" }],
        };

        Resolve(pack).IsSuccess.Should().BeTrue();
    }

    private static RuleDefinition Rule(string id, string[] fromLayers, string[] toLayers) => new()
    {
        Id = id,
        Because = "because",
        Kind = RuleKind.Forbid,
        From = new TargetSelector { Layers = fromLayers },
        To = new TargetSelector { Layers = toLayers },
    };

    private static RuleDefinition RequireRule(string id, string? layer = "Domain", params string[] forLayers) => new()
    {
        Id = id,
        Because = "because",
        Kind = RuleKind.Require,
        From = new TargetSelector { Layers = forLayers, Types = forLayers.Length == 0 ? ["**.X"] : [] },
        Require = new RuleRequirement { Layer = layer },
    };

    [Fact]
    public void RequireLayer_NamingAnUnknownLayer_Fails()
    {
        var pack = new RulePack { Name = "p", Layers = [Layer("Domain")], Rules = [RequireRule("R-1", "Ghost")] };

        var result = Resolve(pack);

        result.IsFailure.Should().BeTrue();
        result.Error.Detail.Should().Contain("rule 'R-1' requires unknown layer 'Ghost'");
    }

    [Fact]
    public void RequireFor_NamingAnUnknownLayer_Fails()
    {
        var pack = new RulePack { Name = "p", Layers = [Layer("Domain")], Rules = [RequireRule("R-1", "Domain", "Ghost")] };

        Resolve(pack).Error.Detail.Should().Contain("rule 'R-1' refers to unknown layer 'Ghost'");
    }

    [Fact]
    public void UniqueNames_UnknownLayerInAgainst_Fails()
    {
        var rule = new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.UniqueNames,
            From = new TargetSelector { Layers = ["Domain"] },
            Against = new TargetSelector { Layers = ["Ghost"] },
        };
        var pack = new RulePack { Name = "p", Layers = [Layer("Domain")], Rules = [rule] };

        Resolve(pack).Error.Detail.Should().Contain("rule 'R-1' refers to unknown layer 'Ghost'");
    }

    [Fact]
    public void RequireRule_NamingKnownLayer_Passes()
    {
        var pack = new RulePack { Name = "p", Layers = [Layer("Domain")], Rules = [RequireRule("R-1", "domain")] };

        Resolve(pack).IsSuccess.Should().BeTrue();
    }

    private static RulePack PackWithRequire(RuleRequirement? requirement) => new()
    {
        Name = "p",
        Layers = [Layer("Domain")],
        Rules =
        [
            new RuleDefinition
            {
                Id = "R-1",
                Because = "because",
                Kind = RuleKind.Require,
                From = new TargetSelector { Types = ["**.X"] },
                Require = requirement,
            },
        ],
    };

    [Fact]
    public void RequireRule_WithoutARequirement_Fails()
    {
        Resolve(PackWithRequire(null)).Error.Detail.Should().Contain("rule 'R-1' is a require rule and has no requirement");
    }

    [Fact]
    public void RequireRule_WithAnEmptyRequirement_Fails()
    {
        Resolve(PackWithRequire(new RuleRequirement())).Error.Detail.Should()
            .Contain("the requirement of rule 'R-1' needs at least one of layer, kind, accessibility, implements, is, isNot");
    }

    [Fact]
    public void RequireRule_WithOnlyIs_Passes()
    {
        Resolve(PackWithRequire(new RuleRequirement { Is = new TargetSelector { Sealed = true } })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RequireRule_WithOnlyIsNot_Passes()
    {
        Resolve(PackWithRequire(new RuleRequirement { IsNot = new TargetSelector { Types = ["**"] } })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RequireRule_WithAnEmptyIs_Fails()
    {
        Resolve(PackWithRequire(new RuleRequirement { Is = new TargetSelector() })).Error.Detail.Should()
            .Contain("the is of rule 'R-1' selects nothing; it needs at least one selector key other than layers and members");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequireRule_ConditionWithLayersOrMembers_Fails(bool layers)
    {
        var condition = layers
            ? new TargetSelector { Layers = ["Domain"], Sealed = true }
            : new TargetSelector { Members = ["**.M"], Sealed = true };

        Resolve(PackWithRequire(new RuleRequirement { IsNot = condition })).Error.Detail.Should()
            .Contain("the isNot of rule 'R-1' has layers or members, which a condition does not take");
    }

    [Fact]
    public void RequireRule_WithUnknownAccessibility_Fails()
    {
        Resolve(PackWithRequire(new RuleRequirement { Accessibility = "private" })).Error.Detail.Should()
            .Contain("the requirement of rule 'R-1' has accessibility 'private', which must be public or internal");
    }

    [Theory]
    [InlineData("public")]
    [InlineData("INTERNAL")]
    public void RequireRule_WithKnownAccessibility_Passes(string accessibility)
    {
        Resolve(PackWithRequire(new RuleRequirement { Accessibility = accessibility })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RequireRule_ImplementsWithOnlyCodeFeatureKeys_Fails()
    {
        var requirement = new RuleRequirement { Implements = new TargetSelector { Interfaces = ["**.IX"] } };

        Resolve(PackWithRequire(requirement)).Error.Detail.Should()
            .Contain("the implements of rule 'R-1' needs at least one of namespaces, assemblies, types");
    }

    [Fact]
    public void RequireRule_ImplementsWithTypes_Passes()
    {
        var requirement = new RuleRequirement { Implements = new TargetSelector { Types = ["**.IX"] } };

        Resolve(PackWithRequire(requirement)).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("CODE-001")]
    [InlineData("code-002")]
    public void RuleId_CODE001_IsReserved(string id)
    {
        var pack = new RulePack { Name = "p", Layers = [Layer("Domain")], Rules = [RequireRule(id)] };

        Resolve(pack).Error.Detail.Should().Contain($"'{id}' is reserved");
    }

    [Theory]
    [InlineData("ROLE-Entity")]
    [InlineData("role-entity")]
    public void RuleId_StartingROLEDash_IsReserved_CaseInsensitive(string id)
    {
        var pack = new RulePack { Name = "p", Layers = [Layer("Domain")], Rules = [RequireRule(id)] };

        Resolve(pack).Error.Detail.Should().Contain($"'{id}' is reserved");
    }

    [Fact]
    public void LayerId_CODE002_IsReserved()
    {
        var layer = new LayerDefinition { Name = "Domain", Id = "CODE-002", Because = "x" };

        Resolve(Pack([layer])).Error.Detail.Should().Contain("'CODE-002' is reserved");
    }

    private static RulePack PackWithRule(RuleDefinition rule) => new() { Name = "p", Layers = [Layer("Domain")], Rules = [rule] };

    private static RuleDefinition ForbidRule(TargetSelector from, TargetSelector? to = null, params RuleException[] exceptions) => new()
    {
        Id = "R-1",
        Because = "because",
        Kind = RuleKind.Forbid,
        From = from,
        To = to ?? new TargetSelector { Types = ["**.T"] },
        Exceptions = exceptions,
    };

    [Theory]
    [InlineData("Public")]
    [InlineData("protectedinternal")]
    [InlineData("")]
    public void TraitAccessibility_OutsideTheSixKeywords_Fails(string accessibility)
    {
        var rule = ForbidRule(new TargetSelector { Types = ["**.X"], Accessibility = [accessibility] });

        var result = Resolve(PackWithRule(rule));

        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Detail.Should().Be(
            $"the from of rule 'R-1' has accessibility '{accessibility}', which must be one of public, internal, protected, private, protected internal, private protected");
    }

    [Fact]
    public void TraitAccessibility_TheSixKeywords_Pass()
    {
        var keywords = new[] { "public", "internal", "protected", "private", "protected internal", "private protected" };
        var rule = ForbidRule(new TargetSelector { Types = ["**.X"], Accessibility = keywords }, new TargetSelector { Types = ["**.T"], Accessibility = keywords });

        Resolve(PackWithRule(rule)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void TraitKinds_NotAnEnumValue_Fails()
    {
        var rule = ForbidRule(new TargetSelector { Types = ["**.X"] }, new TargetSelector { Types = ["**.T"], Kinds = [(CSharpTypeKind)42] });

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be("the to of rule 'R-1' has kind '42', which is not a type kind");
    }

    [Fact]
    public void TraitKinds_EnumAndDelegate_Pass()
    {
        var rule = ForbidRule(new TargetSelector { Kinds = [CSharpTypeKind.Enum, CSharpTypeKind.Delegate] });

        Resolve(PackWithRule(rule)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void UniqueNamesAgainst_InvalidTraits_Fail()
    {
        var rule = new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.UniqueNames,
            From = new TargetSelector { Layers = ["Domain"] },
            Against = new TargetSelector { Layers = ["Domain"], Accessibility = ["open"] },
        };

        Resolve(PackWithRule(rule)).Error.Detail.Should().Contain("the against of rule 'R-1' has accessibility 'open'");
    }

    [Fact]
    public void RequireFor_IsAndIsNot_InvalidTraits_Fail()
    {
        var rule = new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.Require,
            From = new TargetSelector { Types = ["**.X"], Kinds = [(CSharpTypeKind)(-1)] },
            Require = new RuleRequirement
            {
                Is = new TargetSelector { Accessibility = ["Public"] },
                IsNot = new TargetSelector { Kinds = [(CSharpTypeKind)9] },
            },
        };

        Resolve(PackWithRule(rule)).Error.Detail!.Split('\n').Should().Equal(
            "the for of rule 'R-1' has kind '-1', which is not a type kind",
            "the is of rule 'R-1' has accessibility 'Public', which must be one of public, internal, protected, private, protected internal, private protected",
            "the isNot of rule 'R-1' has kind '9', which is not a type kind");
    }

    [Theory]
    [InlineData(CSharpTypeKind.Enum)]
    [InlineData(CSharpTypeKind.Delegate)]
    [InlineData((CSharpTypeKind)42)]
    public void RequireKind_NotBuildable_Fails(CSharpTypeKind kind)
    {
        Resolve(PackWithRequire(new RuleRequirement { Kind = kind })).Error.Detail.Should().Be(
            $"the requirement of rule 'R-1' has kind '{kind}', which must be Class, Interface, Record, Struct or RecordStruct");
    }

    [Fact]
    public void RequireKind_Buildable_Passes()
    {
        Resolve(PackWithRequire(new RuleRequirement { Kind = CSharpTypeKind.RecordStruct })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Exception_InvalidTraits_Fail()
    {
        var rule = ForbidRule(
            new TargetSelector { Types = ["**.X"] },
            null,
            new RuleException { Types = ["**.A"], Because = "a" },
            new RuleException { Accessibility = ["friend"], Kinds = [(CSharpTypeKind)7], Because = "b" });

        Resolve(PackWithRule(rule)).Error.Detail!.Split('\n').Should().Equal(
            "exceptions[1] of rule 'R-1' has kind '7', which is not a type kind",
            "exceptions[1] of rule 'R-1' has accessibility 'friend', which must be one of public, internal, protected, private, protected internal, private protected");
    }

    private const string EmptyEntryReason = "needs 'arguments' or 'named'; to select by the attribute alone, use 'attributes'";

    [Fact]
    public void Exception_WithAnEmptyAttributeArgumentEntry_Fails()
    {
        var rule = ForbidRule(new TargetSelector { Types = ["**.X"] }, null,
            new RuleException { Because = "Empty.", AttributeArguments = [new AttributeArgumentMatch()] });

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be($"attributeArguments[0] in exceptions[0] of rule 'R-1' {EmptyEntryReason}");
    }

    [Fact]
    public void RequireCondition_WithAnEmptyAttributeArgumentEntry_Fails()
    {
        var condition = new TargetSelector { AttributeArguments = [new AttributeArgumentMatch { Attribute = "**.A" }] };

        Resolve(PackWithRequire(new RuleRequirement { Is = condition })).Error.Detail.Should()
            .Contain($"attributeArguments[0] in the is of rule 'R-1' {EmptyEntryReason}");
    }

    [Fact]
    public void Target_WithAnEmptyAttributeArgumentEntry_Fails()
    {
        var rule = ForbidRule(new TargetSelector { AttributeArguments = [new AttributeArgumentMatch()] }, null);

        Resolve(PackWithRule(rule)).Error.Detail.Should().Contain($"attributeArguments[0] in the from of rule 'R-1' {EmptyEntryReason}");
    }

    [Fact]
    public void Exception_SelectingNothing_Fails()
    {
        var rule = ForbidRule(new TargetSelector { Types = ["**.X"] }, null, new RuleException { Because = "Empty." });

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be(
            "exceptions[0] of rule 'R-1' selects nothing; it needs at least one of namespaces, assemblies, types, attributes, attributeArguments, onlyAttributes, a trait, dependsOn or onlyDependsOn");
    }

    [Fact]
    public void Unknown_layer_inside_dependsOn_is_reported_at_its_path()
    {
        var rule = ForbidRule(
            new TargetSelector { Layers = ["Domain"], DependsOn = new TargetSelector { Layers = ["Ghost"] } },
            new TargetSelector { Types = ["**.T"], OnlyDependsOn = new TargetSelector { Layers = ["Nope"] } },
            new RuleException { Because = "x", DependsOn = new TargetSelector { Layers = ["Phantom"] } });

        Resolve(PackWithRule(rule)).Error.Detail!.Split('\n').Should().Equal(
            "the dependsOn of the from of rule 'R-1' refers to unknown layer 'Ghost'",
            "the onlyDependsOn of the to of rule 'R-1' refers to unknown layer 'Nope'",
            "the dependsOn of exceptions[0] of rule 'R-1' refers to unknown layer 'Phantom'");
    }

    [Fact]
    public void Unknown_layer_inside_a_condition_dependency_selector_is_reported_at_its_path()
    {
        var requirement = new RuleRequirement { IsNot = new TargetSelector { OnlyDependsOn = new TargetSelector { Layers = ["Ghost"] } } };

        Resolve(PackWithRequire(requirement)).Error.Detail.Should().Be("the onlyDependsOn of the isNot of rule 'R-1' refers to unknown layer 'Ghost'");
    }

    [Fact]
    public void Selector_and_exception_with_only_dependsOn_resolve()
    {
        var rule = ForbidRule(
            new TargetSelector { DependsOn = new TargetSelector { Layers = ["Domain"] } },
            null,
            new RuleException { Because = "x", OnlyDependsOn = new TargetSelector { Namespaces = ["Shop.**"] } });
        var requirement = new RuleRequirement { Is = new TargetSelector { DependsOn = new TargetSelector { Layers = ["Domain"] } } };

        Resolve(PackWithRule(rule)).IsSuccess.Should().BeTrue();
        Resolve(PackWithRequire(requirement)).IsSuccess.Should().BeTrue();
    }

    public static TheoryData<TargetSelector, string> BadDependencySelectors => new()
    {
        { new TargetSelector(), "the dependsOn of the from of rule 'R-1' selects nothing; it needs at least one selector key other than members, dependsOn and onlyDependsOn" },
        { new TargetSelector { Types = ["**.A"], Members = ["**.M"] },
            "the dependsOn of the from of rule 'R-1' has 'members', which the selector under dependsOn or onlyDependsOn does not take" },
        { new TargetSelector { Types = ["**.A"], DependsOn = new TargetSelector { Types = ["**.B"] } },
            "the dependsOn of the from of rule 'R-1' has 'dependsOn', which the selector under dependsOn or onlyDependsOn does not take" },
        { new TargetSelector { Layers = ["Domain"], Namespaces = ["Shop.**"] },
            "the dependsOn of the from of rule 'R-1' mixes layers with namespaces, assemblies, types, base types, interfaces, attributes, attribute arguments, attribute allow-lists or members" },
        { new TargetSelector { Accessibility = ["friend"] },
            "the dependsOn of the from of rule 'R-1' has accessibility 'friend', which must be one of public, internal, protected, private, protected internal, private protected" },
        { new TargetSelector { AttributeArguments = [new AttributeArgumentMatch()] },
            $"attributeArguments[0] in the dependsOn of the from of rule 'R-1' {EmptyEntryReason}" },
    };

    [Theory]
    [MemberData(nameof(BadDependencySelectors))]
    public void Bad_dependency_selector_is_reported_at_its_path(TargetSelector nested, string failure)
    {
        var rule = ForbidRule(new TargetSelector { Types = ["**.X"], DependsOn = nested });

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be(failure);
    }

    [Fact]
    public void Capture_inside_a_dependency_selector_of_a_slices_rule_fails()
    {
        var rule = new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.Slices,
            From = new TargetSelector(),
            Slices = new SliceDefinition
            {
                Matching = ["Shop.(*).**"],
                Independent = true,
                Allow = new TargetSelector { DependsOn = new TargetSelector { Namespaces = ["Shop.(*).Api.**"] } },
            },
        };

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be(
            "the dependsOn of the allow of rule 'R-1' has a pattern with a capture; a capture, (*) or (**), is only accepted in the matching of a slices rule");
    }

    [Fact]
    public void Implements_with_dependsOn_fails()
    {
        var requirement = new RuleRequirement { Implements = new TargetSelector { Types = ["**.I"], DependsOn = new TargetSelector { Layers = ["Domain"] } } };

        Resolve(PackWithRequire(requirement)).Error.Detail.Should().Be(
            "the implements of rule 'R-1' has 'dependsOn', which implements does not take; it takes namespaces, assemblies, types");
    }

    [Fact]
    public void BannedPackages_exception_with_onlyDependsOn_fails()
    {
        var rule = new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.BannedPackages,
            From = new TargetSelector(),
            Packages = ["FluentValidation"],
            Exceptions = [new RuleException { Because = "x", OnlyDependsOn = new TargetSelector { Layers = ["Domain"] } }],
        };

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be(
            "exceptions[0] of rule 'R-1' has 'onlyDependsOn'; a package belongs to a project, so the exception of a bannedPackages rule names it by 'assemblies'");
    }

    [Fact]
    public void Exception_with_only_attributes_resolves()
    {
        var rule = ForbidRule(new TargetSelector { Types = ["**.X"] }, null, new RuleException { Attributes = ["**.AuditedAttribute"], Because = "Audited." });

        Resolve(PackWithRule(rule)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Exception_with_only_onlyAttributes_resolves()
    {
        var rule = ForbidRule(new TargetSelector { Types = ["**.X"] }, null, new RuleException { OnlyAttributes = ["System.**"], Because = "Plain." });

        Resolve(PackWithRule(rule)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Exception_with_only_named_attribute_arguments_resolves()
    {
        var match = new AttributeArgumentMatch { Named = new Dictionary<string, string>(StringComparer.Ordinal) { ["Legacy"] = "true" } };
        var rule = ForbidRule(new TargetSelector { Types = ["**.X"] }, null, new RuleException { AttributeArguments = [match], Because = "Legacy." });

        Resolve(PackWithRule(rule)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Condition_with_only_onlyAttributes_resolves()
    {
        Resolve(PackWithRequire(new RuleRequirement { Is = new TargetSelector { OnlyAttributes = ["System.**"] } })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Condition_with_only_attribute_arguments_resolves()
    {
        var match = new AttributeArgumentMatch { Arguments = ["1"] };

        Resolve(PackWithRequire(new RuleRequirement { IsNot = new TargetSelector { AttributeArguments = [match] } })).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Slices_allow_with_only_an_attribute_key_resolves(bool only)
    {
        var allow = only
            ? new TargetSelector { OnlyAttributes = ["System.**"] }
            : new TargetSelector { AttributeArguments = [new AttributeArgumentMatch { Arguments = ["1"] }] };

        Resolve(PackWithRule(SlicesRule(Slices(allow)))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Implements_with_attribute_argument_keys_fails()
    {
        var implements = new TargetSelector { Types = ["**.I"], OnlyAttributes = ["System.**"] };

        Resolve(PackWithRequire(new RuleRequirement { Implements = implements })).Error.Detail.Should()
            .Contain("the implements of rule 'R-1' has 'onlyAttributes', which implements does not take");
    }

    [Fact]
    public void Capture_in_the_onlyAttributes_of_a_slices_allow_is_invalid()
    {
        Resolve(PackWithRule(SlicesRule(Slices(new TargetSelector { OnlyAttributes = ["Shop.(*).TagAttribute"] })))).Error.Detail.Should().Be(
            "the allow of rule 'R-1' has a pattern with a capture; a capture, (*) or (**), is only accepted in the matching of a slices rule");
    }

    [Fact]
    public void Capture_in_an_attribute_argument_pattern_of_a_slices_exception_is_invalid()
    {
        var pack = PackWithRule(new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.Slices,
            From = new TargetSelector(),
            Slices = Slices(),
            Exceptions = [new RuleException { AttributeArguments = [new AttributeArgumentMatch { Attribute = "Shop.(*).RouteAttribute", Arguments = ["1"] }], Because = "x" }],
        });

        Resolve(pack).Error.Detail.Should().Be(
            "exceptions[0] of rule 'R-1' has a pattern with a capture; a capture, (*) or (**), is only accepted in the matching of a slices rule");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Exception_WithOnlyATrait_Passes(bool nested)
    {
        var rule = ForbidRule(new TargetSelector { Types = ["**.X"] }, null, new RuleException { Nested = nested, Because = "Nested." });

        Resolve(PackWithRule(rule)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AmendedException_InvalidTraits_Fail()
    {
        var parent = PackWithRule(ForbidRule(new TargetSelector { Types = ["**.X"] }));
        var child = new RulePack
        {
            Name = "child",
            RuleAmendments = [new RuleAmendment { Id = "R-1", Exceptions = [new RuleException { Accessibility = ["Internal"], Because = "x" }] }],
        };

        var result = new Ariadne.NET.RuleEngine.RulePackMerger().Merge([parent, child]);
        result.IsSuccess.Should().BeTrue();

        Resolve(result.Value).Error.Detail.Should().Contain("exceptions[0] of rule 'R-1' has accessibility 'Internal'");
    }

    [Theory]
    [InlineData("namespaces")]
    [InlineData("members")]
    [InlineData("attributes")]
    [InlineData("attributeArguments")]
    [InlineData("onlyAttributes")]
    public void Target_MixingLayersWithGlobKeys_Fails(string key)
    {
        var to = key switch
        {
            "attributeArguments" => new TargetSelector { Layers = ["Domain"], AttributeArguments = [new AttributeArgumentMatch { Arguments = ["1"] }] },
            "onlyAttributes" => new TargetSelector { Layers = ["Domain"], OnlyAttributes = ["**.A"] },
            "namespaces" => new TargetSelector { Layers = ["Domain"], Namespaces = ["X"] },
            "members" => new TargetSelector { Layers = ["Domain"], Members = ["**.M"] },
            _ => new TargetSelector { Layers = ["Domain"], Attributes = ["**.A"] },
        };

        Resolve(PackWithRule(ForbidRule(new TargetSelector { Types = ["**.X"] }, to))).Error.Detail.Should().Be(
            "the to of rule 'R-1' mixes layers with namespaces, assemblies, types, base types, interfaces, attributes, attribute arguments, attribute allow-lists or members");
    }

    [Fact]
    public void Target_LayersWithTraits_Passes()
    {
        var rule = ForbidRule(new TargetSelector { Layers = ["Domain"], Sealed = true, Accessibility = ["public"] });

        Resolve(PackWithRule(rule)).IsSuccess.Should().BeTrue();
    }

    private static RuleDefinition SlicesRule(SliceDefinition? slices) => new()
    {
        Id = "R-1",
        Because = "because",
        Kind = RuleKind.Slices,
        From = new TargetSelector(),
        Slices = slices,
    };

    private static SliceDefinition Slices(TargetSelector? allow = null, string pattern = "Shop.(*).**", bool independent = true) => new()
    {
        Matching = [pattern],
        Independent = independent,
        FreeOfCycles = !independent,
        Allow = allow,
    };

    [Fact]
    public void Slices_rule_with_known_layer_in_allow_passes()
    {
        Resolve(PackWithRule(SlicesRule(Slices(new TargetSelector { Layers = ["domain"] })))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Unknown_layer_in_allow_is_invalid()
    {
        Resolve(PackWithRule(SlicesRule(Slices(new TargetSelector { Layers = ["Ghost"] })))).Error.Detail
            .Should().Be("rule 'R-1' refers to unknown layer 'Ghost'");
    }

    [Fact]
    public void Slices_rule_without_slices_is_invalid()
    {
        Resolve(PackWithRule(SlicesRule(null))).Error.Detail.Should().Be("rule 'R-1' is a slices rule and has no slices");
    }

    [Fact]
    public void Slices_rule_with_an_invalid_pattern_is_invalid()
    {
        Resolve(PackWithRule(SlicesRule(Slices(pattern: "Shop.**")))).Error.Detail.Should().Be(
            "the slices of rule 'R-1' has an invalid pattern: pattern 'Shop.**' must have exactly one capture, (*) or (**)");
    }

    [Fact]
    public void Slices_rule_with_both_conditions_is_invalid()
    {
        var slices = new SliceDefinition { Matching = ["Shop.(*)"], Independent = true, FreeOfCycles = true };

        Resolve(PackWithRule(SlicesRule(slices))).Error.Detail.Should().Be(
            "the slices of rule 'R-1' must set exactly one of freeOfCycles and independent to true");
    }

    [Fact]
    public void Slices_rule_with_empty_matching_is_invalid()
    {
        var slices = new SliceDefinition { Matching = [], Independent = true };

        Resolve(PackWithRule(SlicesRule(slices))).Error.Detail.Should().Be("the slices of rule 'R-1' needs at least one entry in matching");
    }

    [Fact]
    public void Slices_rule_with_a_null_matching_entry_is_invalid()
    {
        var slices = new SliceDefinition { Matching = ["Shop.(*)", null!], Independent = true };

        Resolve(PackWithRule(SlicesRule(slices))).Error.Detail.Should().Be("the slices of rule 'R-1' has a null entry in matching");
    }

    [Fact]
    public void Slices_rule_with_null_matching_is_invalid()
    {
        var slices = new SliceDefinition { Matching = null!, Independent = true };

        Resolve(PackWithRule(SlicesRule(slices))).Error.Detail.Should().Be("the slices of rule 'R-1' needs at least one entry in matching");
    }

    [Fact]
    public void Capture_in_an_exception_of_a_slices_rule_is_invalid()
    {
        var rule = new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.Slices,
            From = new TargetSelector(),
            Slices = Slices(),
            Exceptions = [new RuleException { Namespaces = ["Shop.(*).Legacy"], Because = "x" }],
        };

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be(
            "exceptions[0] of rule 'R-1' has a pattern with a capture; a capture, (*) or (**), is only accepted in the matching of a slices rule");
    }

    [Fact]
    public void Capture_in_exception_attributes_of_a_slices_rule_is_invalid()
    {
        var rule = new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.Slices,
            From = new TargetSelector(),
            Slices = Slices(),
            Exceptions = [new RuleException { Attributes = ["Shop.(*).LegacyAttribute"], Because = "x" }],
        };

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be(
            "exceptions[0] of rule 'R-1' has a pattern with a capture; a capture, (*) or (**), is only accepted in the matching of a slices rule");
    }

    [Fact]
    public void Capture_in_an_amendment_exception_of_a_slices_rule_is_invalid()
    {
        var pack = new RulePack
        {
            Name = "child",
            Extends = ["builtin://modular-monolith"],
            RuleAmendments = [new RuleAmendment { Id = "MM-MODULES-INDEPENDENT", Exceptions = [new RuleException { Namespaces = ["Shop.(**).Legacy"], Because = "x" }] }],
        };

        Resolve(pack).Error.Detail.Should().Be(
            "exceptions[0] of rule 'MM-MODULES-INDEPENDENT' has a pattern with a capture; a capture, (*) or (**), is only accepted in the matching of a slices rule");
    }

    [Fact]
    public void Capture_in_the_allow_of_a_slices_rule_is_invalid()
    {
        Resolve(PackWithRule(SlicesRule(Slices(new TargetSelector { Namespaces = ["Shop.(**).Public"] })))).Error.Detail.Should().Be(
            "the allow of rule 'R-1' has a pattern with a capture; a capture, (*) or (**), is only accepted in the matching of a slices rule");
    }

    [Theory]
    [InlineData("members", "the allow of rule 'R-1' has members, which it does not take")]
    [InlineData("empty", "the allow of rule 'R-1' selects nothing; it needs at least one selector key")]
    [InlineData("mixed", "the allow of rule 'R-1' mixes layers with namespaces, assemblies, types, base types, interfaces, attributes, attribute arguments, attribute allow-lists or members")]
    public void Slices_rule_with_an_unusable_allow_is_invalid(string shape, string expected)
    {
        var allow = shape switch
        {
            "members" => new TargetSelector { Members = ["**.Get"] },
            "empty" => new TargetSelector(),
            _ => new TargetSelector { Layers = ["Domain"], Namespaces = ["X"] },
        };

        Resolve(PackWithRule(SlicesRule(Slices(allow)))).Error.Detail.Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UniqueNamesTarget_WithATrait_Fails(bool onAgainst)
    {
        var plain = new TargetSelector { Layers = ["Domain"] };
        var withTrait = new TargetSelector { Layers = ["Domain"], Sealed = true };
        var rule = new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.UniqueNames,
            From = onAgainst ? plain : withTrait,
            Against = onAgainst ? withTrait : plain,
        };

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be(
            $"the {(onAgainst ? "against" : "for")} of rule 'R-1' has 'sealed', which a uniqueNames target does not take");
    }

    [Fact]
    public void UniqueNames_for_is_named_for_when_it_mixes_layers_with_patterns()
    {
        var rule = new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.UniqueNames,
            From = new TargetSelector { Layers = ["Domain"], Namespaces = ["Shop.**"] },
            Against = new TargetSelector { Layers = ["Domain"] },
        };

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be(
            "the for of rule 'R-1' mixes layers with namespaces, assemblies, types, base types, interfaces, attributes, attribute arguments, attribute allow-lists or members");
    }

    [Fact]
    public void UniqueNames_for_is_named_for_in_a_bad_dependency_selector()
    {
        var rule = new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.UniqueNames,
            From = new TargetSelector { Layers = ["Domain"], DependsOn = new TargetSelector() },
            Against = new TargetSelector { Layers = ["Domain"] },
        };

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be(
            "the dependsOn of the for of rule 'R-1' selects nothing; it needs at least one selector key other than members, dependsOn and onlyDependsOn");
    }

    [Fact]
    public void RequireRule_ImplementsWithAKeyBeyondNamespacesAssembliesTypes_Fails()
    {
        var requirement = new RuleRequirement { Implements = new TargetSelector { Types = ["**.IX"], Names = ["Legacy*"], Interfaces = ["**.IY"] } };

        Resolve(PackWithRequire(requirement)).Error.Detail!.Split('\n').Should().Equal(
            "the implements of rule 'R-1' has 'interfaces', which implements does not take; it takes namespaces, assemblies, types",
            "the implements of rule 'R-1' has 'names', which implements does not take; it takes namespaces, assemblies, types");
    }

    [Fact]
    public void BannedPackagesException_WithATrait_Fails()
    {
        var rule = new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.BannedPackages,
            Packages = ["Newtonsoft.Json"],
            From = new TargetSelector { Layers = ["Domain"] },
            Exceptions = [new RuleException { Assemblies = ["Shop.Domain"], Nested = true, Because = "x" }],
        };

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be(
            "exceptions[0] of rule 'R-1' has 'nested'; a package belongs to a project, so the exception of a bannedPackages rule names it by 'assemblies'");
    }

    [Fact]
    public void UniqueNamesException_WithAssemblies_Fails()
    {
        var rule = new RuleDefinition
        {
            Id = "R-1",
            Because = "because",
            Kind = RuleKind.UniqueNames,
            From = new TargetSelector { Layers = ["Domain"] },
            Against = new TargetSelector { Layers = ["Domain"] },
            Exceptions = [new RuleException { Assemblies = ["Shop.Domain"], Because = "x" }],
        };

        Resolve(PackWithRule(rule)).Error.Detail.Should().Be(
            "exceptions[0] of rule 'R-1' has 'assemblies'; an exception of a uniqueNames rule excuses a type's name, so it takes 'namespaces', 'types', 'attributes', 'attributeArguments', 'onlyAttributes', a type trait, 'dependsOn' or 'onlyDependsOn'");
    }
}
