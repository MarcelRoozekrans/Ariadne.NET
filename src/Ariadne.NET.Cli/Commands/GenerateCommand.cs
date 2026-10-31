using System.CommandLine;
using Ariadne.NET.Orchestration;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Commands;

public static class GenerateCommand
{
    public static Command Create(IServiceProvider provider)
    {
        var roleOption = new Option<string>("--role", "-r")
        {
            Description = "Component role to generate (e.g. Entity, Repository)",
            Required = true,
        };

        var nameOption = new Option<string>("--name", "-n")
        {
            Description = "Name of the component to generate (e.g. Order)",
            Required = true,
        };

        var solutionOption = new Option<string>("--solution", "-s")
        {
            Description = "Solution name",
            Required = true,
        };

        var featureOption = new Option<string>("--feature", "-f")
        {
            Description = "Feature name",
            Required = true,
        };

        var command = new Command("generate", "Generate code from architecture rules")
        {
            roleOption,
            nameOption,
            solutionOption,
            featureOption,
        };

        command.SetAction(parseResult =>
        {
            var role = parseResult.GetValue(roleOption)!;
            var name = parseResult.GetValue(nameOption)!;
            var solution = parseResult.GetValue(solutionOption)!;
            var feature = parseResult.GetValue(featureOption)!;

            var result = ExecuteGeneration(provider, role, name, solution, feature);
            PrintResult(result);
            return result.Success ? ExitCodes.Success : ExitCodes.Error;
        });

        return command;
    }

    private static GenerateResult ExecuteGeneration(
        IServiceProvider provider,
        string role,
        string name,
        string solution,
        string feature)
    {
        var pack = RulesCommand.ResolveRulesFromFile(provider);
        var orchestrator = provider.GetRequiredService<IGenerateOrchestrator>();

        var request = new GenerateRequest
        {
            SolutionName = solution,
            Feature = feature,
            Components = [new ComponentRequest { RoleName = role, Name = name }],
        };

        return orchestrator.Generate(pack, request);
    }

    private static void PrintResult(GenerateResult result)
    {
        if (!result.Success)
        {
            Console.Error.WriteLine("Generation failed:");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }

            return;
        }

        foreach (var file in result.GeneratedFiles)
        {
            Console.WriteLine($"// File: {file.FilePath}");
            Console.WriteLine($"// Type: {file.Namespace}.{file.TypeName}");
            Console.WriteLine();
            Console.WriteLine(file.Code);
            Console.WriteLine();
        }

        Console.WriteLine($"Generated {result.GeneratedFiles.Count} file(s).");
    }
}
