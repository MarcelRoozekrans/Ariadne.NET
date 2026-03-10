using System.CommandLine;

namespace ArchGov.Host.Cli.Commands;

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
            _ = RulesCommand.ResolveRulesFromFile(provider);

            Console.WriteLine($"Validation of '{path}' is not yet implemented.");
            Console.WriteLine("This feature will be available in a future release.");
        });

        return command;
    }
}
