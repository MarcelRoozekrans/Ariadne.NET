using System.CommandLine;
using Ariadne.NET.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Results;

namespace Ariadne.NET.Cli.Commands;

public static class RulesCommand
{
    public static Command Create(IServiceProvider provider)
    {
        var command = new Command("rules", "Print resolved architecture rules");

        command.SetAction(_ =>
        {
            var resolved = ResolveRulesFromFile(provider);
            if (resolved.IsFailure)
            {
                return ErrorOutput.Report(resolved.Error);
            }

            var pack = resolved.Value;

            Console.WriteLine("Resolved Architecture Rules");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine();

            Console.WriteLine("Layers:");
            foreach (var layer in pack.Layers)
            {
                var deps = layer.AllowedDependencies.Count > 0
                    ? string.Join(", ", layer.AllowedDependencies)
                    : "nothing";
                var selectors = Describe(layer.Namespaces, layer.Assemblies);
                Console.WriteLine($"  {layer.Name} [{layer.Id}]: {selectors}; depends on {deps}");
            }

            Console.WriteLine();
            Console.WriteLine("Rules:");
            if (pack.Rules.Count == 0)
            {
                Console.WriteLine("  (none)");
            }

            foreach (var rule in pack.Rules)
            {
                var kind = rule.Kind == RuleKind.Forbid ? "forbid" : "allowOnly";
                Console.WriteLine($"  {rule.Id} {kind} {Describe(rule.From)} -> {Describe(rule.To)}");
                if (rule.Exceptions.Count > 0)
                {
                    Console.WriteLine($"    {rule.Exceptions.Count} exception(s)");
                }

                Console.WriteLine($"    {rule.Because}");
            }

            Console.WriteLine();
            Console.WriteLine($"Component Roles: {pack.ComponentRoles.Count}");
            Console.WriteLine($"Naming - Namespace: {pack.NamingConventions.Namespace}");
            Console.WriteLine($"Naming - FilePath:  {pack.NamingConventions.FilePath}");
            return ExitCodes.Success;
        });

        return command;
    }

    internal static string Describe(TargetSelector selector) =>
        Describe(selector.Namespaces, selector.Assemblies, selector.Layers, selector.Types);

    // Kinds in the order layers, namespaces, assemblies, types; kinds joined by "; ", values by ", ".
    private static string Describe(
        IList<string> namespaces,
        IList<string> assemblies,
        IList<string>? layers = null,
        IList<string>? types = null)
    {
        var parts = new List<string>();
        AddKind(parts, "layers", layers);
        AddKind(parts, "namespaces", namespaces);
        AddKind(parts, "assemblies", assemblies);
        AddKind(parts, "types", types);
        return parts.Count > 0 ? string.Join("; ", parts) : "no selectors";
    }

    private static void AddKind(List<string> parts, string label, IList<string>? values)
    {
        if (values is { Count: > 0 })
        {
            parts.Add($"{label} {string.Join(", ", values)}");
        }
    }

    internal static Result<RulePack, AriadneError> ResolveRulesFromFile(IServiceProvider provider)
    {
        const string rulesPath = ".architecture/rules.yaml";

        if (!File.Exists(rulesPath))
        {
            return Result<RulePack, AriadneError>.Failure(new AriadneError(
                AriadneErrorCode.NotFound,
                "No .architecture/rules.yaml found. Run 'ariadne init' first.",
                rulesPath));
        }

        var loader = provider.GetRequiredService<IRulePackLoader>();
        var resolver = provider.GetRequiredService<IRulePackResolver>();

        var userPack = loader.LoadFromFile(rulesPath);
        if (userPack.IsFailure)
        {
            return userPack;
        }

        return resolver.Resolve(userPack.Value, Directory.GetCurrentDirectory());
    }
}
