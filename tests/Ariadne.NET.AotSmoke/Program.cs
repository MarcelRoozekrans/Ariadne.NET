using Ariadne.NET.Abstractions;

// Roots Ariadne.NET.Abstractions in a Native AOT publish: any trim or AOT warning fails the build.
var pack = new RulePack
{
    Name = "aot-smoke",
    Layers = [new LayerDefinition { Name = "Domain" }],
    ComponentRoles = [new ComponentRoleDefinition { Name = "Entity", Layer = "Domain" }],
};

Console.WriteLine($"{pack.Name}: {pack.Layers.Count} layer(s), {pack.ComponentRoles.Count} role(s)");
return 0;
