using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

// Roots Ariadne.NET.Abstractions in a Native AOT publish: any trim or AOT warning fails the build.
var pack = new RulePack
{
    Name = "aot-smoke",
    Layers = [new LayerDefinition { Name = "Domain", Id = "AOT-DOMAIN", Because = "Smoke layer.", Namespaces = ["Domain.**"] }],
    ComponentRoles = [new ComponentRoleDefinition { Name = "Entity", Layer = "Domain" }],
    Rules =
    [
        new RuleDefinition
        {
            Id = "no-domain-to-infra",
            Because = "Domain must stay free of infrastructure.",
            Kind = RuleKind.Forbid,
            From = new TargetSelector { Layers = ["Domain"] },
            To = new TargetSelector { Namespaces = ["Infrastructure.*"] },
            Exceptions = [new RuleException { Types = ["Aot.Legacy"], Because = "Smoke exception." }],
        },
        new RuleDefinition
        {
            Id = "handlers-in-domain",
            Because = "Handlers belong in the domain layer.",
            Kind = RuleKind.Require,
            From = new TargetSelector { Interfaces = ["IHandler"] },
            Require = new RuleRequirement { Layer = "Domain", Kind = CSharpTypeKind.Class, Accessibility = "public", Implements = new TargetSelector { Types = ["IHandler"] } },
        },
        new RuleDefinition
        {
            Id = "no-json-net",
            Because = "One JSON library.",
            Kind = RuleKind.BannedPackages,
            From = new TargetSelector { Assemblies = ["Domain"] },
            Packages = ["Newtonsoft.Json"],
        },
        new RuleDefinition
        {
            Id = "distinct-names",
            Because = "Domain names stay apart from infrastructure names.",
            Kind = RuleKind.UniqueNames,
            From = new TargetSelector { Layers = ["Domain"] },
            Against = new TargetSelector { Namespaces = ["Infrastructure.**"] },
        },
    ],
};

var validator = new RulePackValidator(
    new LayerDefinitionValidator(),
    new ComponentRoleDefinitionValidator(),
    new RuleDefinitionValidator(new TargetSelectorValidator(), new TargetSelectorValidator(), new RuleRequirementValidator(new TargetSelectorValidator()), new TargetSelectorValidator(), new RuleExceptionValidator()),
    new LayerRetargetValidator(),
    new RuleAmendmentValidator(new RuleExceptionValidator()),
    new NamingConventionsValidator());
var validation = validator.Validate(pack);

var result = validation.IsValid
    ? Result<RulePack, AriadneError>.Success(pack)
    : Result<RulePack, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, "Rule pack 'aot-smoke' is invalid."));

Console.WriteLine($"{pack.Name}: {pack.Layers.Count} layer(s), {pack.ComponentRoles.Count} role(s), {pack.Rules.Count} rule(s), valid: {validation.IsValid}, success: {result.IsSuccess}");
return result.IsSuccess ? 0 : 1;
