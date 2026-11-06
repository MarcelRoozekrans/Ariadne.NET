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
                PrintRule(rule);
            }

            Console.WriteLine();
            Console.WriteLine($"Component Roles: {pack.ComponentRoles.Count}");
            Console.WriteLine($"Naming - Namespace: {pack.NamingConventions.Namespace}");
            Console.WriteLine($"Naming - FilePath:  {pack.NamingConventions.FilePath}");
            return ExitCodes.Success;
        });

        return command;
    }

    private static void PrintRule(RuleDefinition rule)
    {
        var kind = rule.Kind switch
        {
            RuleKind.Forbid => "forbid",
            RuleKind.AllowOnly => "allowOnly",
            RuleKind.Require => "require",
            _ => rule.Kind.ToString(),
        };
        var target = rule.Kind == RuleKind.Require ? Describe(rule.Require) : Describe(rule.To);
        Console.WriteLine($"  {rule.Id} {kind} {Describe(rule.From)} -> {target}");
        if (rule.Exceptions.Count > 0)
        {
            Console.WriteLine($"    {rule.Exceptions.Count} exception(s)");
        }

        Console.WriteLine($"    {rule.Because}");
    }

    internal static string Describe(TargetSelector selector) =>
        Describe(
            selector.Namespaces,
            selector.Assemblies,
            selector.Layers,
            selector.Types,
            selector.BaseTypes,
            selector.Interfaces,
            selector.Attributes);

    // The enum name with a lower-case first letter is the spelling a pack uses: class, recordStruct.
    private static string YamlSpelling(CSharpTypeKind kind)
    {
        var name = kind.ToString();
        return string.Concat(char.ToLowerInvariant(name[0]).ToString(), name.AsSpan(1));
    }

    // Members in the order layer, kind, accessibility, implements, joined by ", ".
    internal static string Describe(RuleRequirement? requirement)
    {
        if (requirement is null)
        {
            return "no requirement";
        }

        var parts = new List<string>();
        if (requirement.Layer is not null)
        {
            parts.Add($"layer {requirement.Layer}");
        }

        if (requirement.Kind is { } kind)
        {
            parts.Add($"kind {YamlSpelling(kind)}");
        }

        if (requirement.Accessibility is not null)
        {
            parts.Add(requirement.Accessibility);
        }

        if (requirement.Implements is not null)
        {
            parts.Add($"implements {Describe(requirement.Implements)}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "no requirement";
    }

    // Kinds in the order layers, namespaces, assemblies, types, baseTypes, interfaces, attributes; kinds joined by "; ", values by ", ".
    private static string Describe(
        IList<string> namespaces,
        IList<string> assemblies,
        IList<string>? layers = null,
        IList<string>? types = null,
        IList<string>? baseTypes = null,
        IList<string>? interfaces = null,
        IList<string>? attributes = null)
    {
        var parts = new List<string>();
        AddKind(parts, "layers", layers);
        AddKind(parts, "namespaces", namespaces);
        AddKind(parts, "assemblies", assemblies);
        AddKind(parts, "types", types);
        AddKind(parts, "baseTypes", baseTypes);
        AddKind(parts, "interfaces", interfaces);
        AddKind(parts, "attributes", attributes);
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
