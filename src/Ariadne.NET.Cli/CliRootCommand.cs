using System.Diagnostics;
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

        // The default exception handler exits with 1, which would read as "violations found".
        try
        {
            return parseResult.Invoke(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Debug.WriteLine(ex);
            return ExitCodes.Error;
        }
    }
}
