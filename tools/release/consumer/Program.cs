using Ariadne.NET.Abstractions;
using Ariadne.NET.Validation;
using Microsoft.Extensions.DependencyInjection;

using var services = new ServiceCollection().AddAriadneNETServices().BuildServiceProvider();
var loader = services.GetRequiredService<IRulePackLoader>();
var resolver = services.GetRequiredService<IRulePackResolver>();
var validator = services.GetRequiredService<ISourceValidator>();

// A rule pack that builds on the built-in clean-architecture pack.
var pack = loader.LoadFromString("""
    name: my-service
    extends:
      - builtin://clean-architecture
    """);
if (pack.IsFailure)
{
    Console.Error.WriteLine(pack.Error.Message);
    return 1;
}

var resolved = resolver.Resolve(pack.Value);
if (resolved.IsFailure)
{
    Console.Error.WriteLine(resolved.Error.Message);
    return 1;
}

Console.WriteLine($"{resolved.Value.Name}: {resolved.Value.Layers.Count} layers, {resolved.Value.ComponentRoles.Count} component roles");
foreach (var layer in resolved.Value.Layers)
{
    Console.WriteLine($"  {layer.Name} -> [{string.Join(", ", layer.AllowedDependencies)}]");
}

// Check the C# files under a directory against the pack: the first argument, else the current directory.
var result = validator.Validate(resolved.Value, args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
Console.WriteLine($"Checked {result.FilesScanned} files: {result.Violations.Count} violations");
foreach (var violation in result.Violations)
{
    Console.WriteLine($"  {violation.ViolationType} in {violation.FilePath}: {violation.Message}");
}

return result.IsValid ? 0 : 1;
