using System.CommandLine;
using System.CommandLine.Parsing;
using Ariadne.NET.Cli.Commands;

namespace Ariadne.NET.Cli;

public static class CliRootCommand
{
    public static RootCommand Create(IServiceProvider provider)
    {
        var rootCommand = new RootCommand("Ariadne.NET CLI");

        rootCommand.Subcommands.Add(InitCommand.Create(provider));
        rootCommand.Subcommands.Add(RulesCommand.Create(provider));
        rootCommand.Subcommands.Add(RolesCommand.Create(provider));
        rootCommand.Subcommands.Add(GenerateCommand.Create(provider));
        rootCommand.Subcommands.Add(ValidateCommand.Create(provider));
        rootCommand.Subcommands.Add(SyncCommand.Create(provider));
        rootCommand.Subcommands.Add(HookCommand.Create(provider));

        return rootCommand;
    }

    public static async Task<int> InvokeAsync(IServiceProvider provider, string[] args, CancellationToken cancellationToken = default)
    {
        // Expected failures come back as results with their own exit code. This handler is for unexpected
        // exceptions only, and the default handler would exit with 1, which reads as "violations found".
        try
        {
            var parseResult = CommandLineParser.Parse(Create(provider), args, new ParserConfiguration());
            return await parseResult.InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested || cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine("Cancelled.");
            return ExitCodes.Error;
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
