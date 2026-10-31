using System.CommandLine;
using System.CommandLine.Parsing;
using Ariadne.NET.Cli.Commands;

namespace Ariadne.NET.Cli;

public static class CliRootCommand
{
    public static RootCommand Create(IServiceProvider provider)
    {
        var rootCommand = new RootCommand("Architecture Governance Engine CLI");

        rootCommand.Subcommands.Add(InitCommand.Create(provider));
        rootCommand.Subcommands.Add(RulesCommand.Create(provider));
        rootCommand.Subcommands.Add(RolesCommand.Create(provider));
        rootCommand.Subcommands.Add(GenerateCommand.Create(provider));
        rootCommand.Subcommands.Add(ValidateCommand.Create(provider));
        rootCommand.Subcommands.Add(LintCommand.Create(provider));
        rootCommand.Subcommands.Add(SyncCommand.Create(provider));

        return rootCommand;
    }

    public static int Invoke(IServiceProvider provider, string[] args)
    {
        var parseResult = CommandLineParser.Parse(Create(provider), args, new ParserConfiguration());

        // Expected failures come back as results with their own exit code. This handler is for unexpected
        // exceptions only, and the default handler would exit with 1, which reads as "violations found".
        try
        {
            return parseResult.Invoke(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
        }
        catch (Exception ex)
        {
            ReportUnexpected(ex);
            return ExitCodes.Error;
        }
    }

    private static void ReportUnexpected(Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Console.Error.WriteLine(ex.ToString());
    }
}
