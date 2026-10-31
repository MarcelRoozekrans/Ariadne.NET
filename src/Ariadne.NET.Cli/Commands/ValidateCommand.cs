using System.CommandLine;
using Ariadne.NET.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Commands;

public static class ValidateCommand
{
    public static Command Create(IServiceProvider provider)
    {
        var pathArgument = new Argument<string>("path")
        {
            Description = "Path to the source directory to validate",
            DefaultValueFactory = _ => "./src",
        };

        var command = new Command("validate", "Validate source code against architecture rules")
        {
            pathArgument,
        };

        command.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathArgument)!;
            var resolvedPack = RulesCommand.ResolveRulesFromFile(provider);
            var validator = provider.GetRequiredService<ISourceValidator>();

            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                Console.Error.WriteLine($"Error: Directory '{fullPath}' does not exist.");
                return ExitCodes.Error;
            }

            Console.WriteLine($"Validating source files in: {fullPath}");
            Console.WriteLine();

            var result = validator.Validate(resolvedPack, fullPath);

            Console.WriteLine($"Files scanned: {result.FilesScanned}");

            if (result.IsValid)
            {
                Console.WriteLine("Result: PASS - No violations found.");
                return ExitCodes.Success;
            }
            else
            {
                Console.WriteLine($"Result: FAIL - {result.Violations.Count} violation(s) found.");
                Console.WriteLine();

                foreach (var violation in result.Violations)
                {
                    Console.WriteLine($"  [{violation.ViolationType}] {violation.FilePath}");
                    Console.WriteLine($"    {violation.Message}");
                }

                return ExitCodes.Violations;
            }
        });

        return command;
    }
}
