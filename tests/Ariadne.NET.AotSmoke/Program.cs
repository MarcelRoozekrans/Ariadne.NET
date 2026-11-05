using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

// Roots Ariadne.NET.Abstractions in a Native AOT publish: any trim or AOT warning fails the build.
var pack = new RulePack
{
    Name = "aot-smoke",
    Layers = [new LayerDefinition { Name = "Domain" }],
    ComponentRoles = [new ComponentRoleDefinition { Name = "Entity", Layer = "Domain" }],
};

var validator = new RulePackValidator(
    new LayerDefinitionValidator(),
    new ComponentRoleDefinitionValidator(),
    new CodeFeatureRuleDefinitionValidator(),
    new NamingConventionsValidator());
var validation = validator.Validate(pack);

var result = validation.IsValid
    ? Result<RulePack, AriadneError>.Success(pack)
    : Result<RulePack, AriadneError>.Failure(new AriadneError(AriadneErrorCode.Invalid, "Rule pack 'aot-smoke' is invalid."));

Console.WriteLine($"{pack.Name}: {pack.Layers.Count} layer(s), {pack.ComponentRoles.Count} role(s), valid: {validation.IsValid}, success: {result.IsSuccess}");
return result.IsSuccess ? 0 : 1;
