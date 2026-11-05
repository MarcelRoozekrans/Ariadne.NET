using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

// Roots Ariadne.NET.Abstractions in a Native AOT publish: any trim or AOT warning fails the build.
var pack = new RulePack
{
    Name = "aot-smoke",
    Layers = [new LayerDefinition { Name = "Domain" }],
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
    ],
};

var validator = new RulePackValidator(
    new LayerDefinitionValidator(),
    new ComponentRoleDefinitionValidator(),
    new CodeFeatureRuleDefinitionValidator(),
    new RuleDefinitionValidator(new TargetSelectorValidator(), new TargetSelectorValidator(), new RuleExceptionValidator()),
    new LayerRetargetValidator(),
    new RuleAmendmentValidator(new RuleExceptionValidator()),
    new NamingConventionsValidator());
var validation = validator.Validate(pack);

var result = validation.IsValid
    ? Result<RulePack, AriadneError>.Success(pack)
    : Result<RulePack, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, "Rule pack 'aot-smoke' is invalid."));

Console.WriteLine($"{pack.Name}: {pack.Layers.Count} layer(s), {pack.ComponentRoles.Count} role(s), {pack.Rules.Count} rule(s), valid: {validation.IsValid}, success: {result.IsSuccess}");
return result.IsSuccess ? 0 : 1;
