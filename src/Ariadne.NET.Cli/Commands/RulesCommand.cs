using System.CommandLine;
using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
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
                Console.WriteLine($"  {RulePackDescriber.DescribeLayer(layer)}");
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
        var lines = RulePackDescriber.DescribeRule(rule);
        Console.WriteLine($"  {lines[0]}");
        foreach (var line in lines.Skip(1))
        {
            Console.WriteLine($"    {line}");
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
