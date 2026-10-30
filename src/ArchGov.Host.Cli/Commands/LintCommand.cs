using System.CommandLine;
using ArchGov.Core.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace ArchGov.Host.Cli.Commands;

public static class LintCommand
{
    public static Command Create(IServiceProvider provider)
    {
        var pathArgument = new Argument<string>("path")
        {
            Description = "Path to the source directory to lint",
            DefaultValueFactory = _ => "./src",
        };

        var command = new Command("lint", "Check naming, namespace, and file path compliance")
        {
            pathArgument,
        };

        command.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathArgument)!;
            var resolvedPack = RulesCommand.ResolveRulesFromFile(provider);
            var linter = provider.GetRequiredService<ILinter>();

            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                Console.Error.WriteLine($"Error: Directory '{fullPath}' does not exist.");
                return ExitCodes.Error;
            }

            Console.WriteLine($"Linting source files in: {fullPath}");
            Console.WriteLine();

            var result = linter.Lint(resolvedPack, fullPath);

            PrintResult(result);
            return result.IsClean ? ExitCodes.Success : ExitCodes.Violations;
        });

        return command;
    }

    private static void PrintResult(LintResult result)
    {
        Console.WriteLine($"Files scanned: {result.FilesScanned}");

        if (result.IsClean)
        {
            Console.WriteLine("Result: CLEAN - No issues found.");
            return;
        }

        Console.WriteLine($"Result: {result.Violations.Count} issue(s) found.");
        Console.WriteLine();

        foreach (var violation in result.Violations)
        {
            Console.WriteLine($"  [{violation.Category}] {violation.FilePath}");
            Console.WriteLine($"    {violation.Message}");
        }
    }
}
