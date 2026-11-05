using Ariadne.NET.Abstractions;
using Ariadne.NET.Workspaces;
using Microsoft.Extensions.DependencyInjection;

using var services = new ServiceCollection().AddAriadneNETServices().AddAriadneNETWorkspaces().BuildServiceProvider();
var loader = services.GetRequiredService<IRulePackLoader>();
var resolver = services.GetRequiredService<IRulePackResolver>();
var validator = services.GetRequiredService<ISolutionValidator>();

// A rule pack that builds on the built-in clean-architecture pack.
var pack = loader.LoadFromString("""
    schema: 2
    name: my-service
    extends:
      - builtin://clean-architecture
    """);
if (pack.IsFailure)
{
    Console.Error.WriteLine(pack.Error.Message);
    return 2;
}

var resolved = resolver.Resolve(pack.Value);
if (resolved.IsFailure)
{
    Console.Error.WriteLine(resolved.Error.Message);
    return 2;
}

Console.WriteLine($"{resolved.Value.Name}: {resolved.Value.Layers.Count} layers, {resolved.Value.ComponentRoles.Count} component roles");
foreach (var layer in resolved.Value.Layers)
{
    Console.WriteLine($"  {layer.Name} -> [{string.Join(", ", layer.AllowedDependencies)}]");
}

// Check a solution or project against the pack: the first argument, else the current directory.
var result = await validator.ValidateAsync(resolved.Value, args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
if (result.IsFailure)
{
    Console.Error.WriteLine(result.Error.Message);
    return 2;
}

var report = result.Value;
Console.WriteLine($"Checked {report.TypesChecked} types: {report.Violations.Count} violations");
foreach (var violation in report.Violations)
{
    var where = violation.Location is { } location ? $"{location.FilePath}:{location.Line}" : violation.SourceType;
    Console.WriteLine($"  [{violation.Kind}] {where}: {violation.Message}");
}

return report.IsValid ? 0 : 1;
