using System.CommandLine;
using System.CommandLine.Parsing;
using Ariadne.NET.Cli.Commands;

namespace Ariadne.NET.Cli;

public static class CliRootCommand
{
    public static RootCommand Create(IServiceProvider provider) => Create(provider, HookCommand.Create(provider));

    internal static RootCommand Create(IServiceProvider provider, Command hookCommand)
    {
        var rootCommand = new RootCommand("Ariadne.NET CLI");

        rootCommand.Subcommands.Add(InitCommand.Create(provider));
        rootCommand.Subcommands.Add(RulesCommand.Create(provider));
        rootCommand.Subcommands.Add(RolesCommand.Create(provider));
        rootCommand.Subcommands.Add(GenerateCommand.Create(provider));
        rootCommand.Subcommands.Add(ValidateCommand.Create(provider));
        rootCommand.Subcommands.Add(SyncCommand.Create(provider));
        rootCommand.Subcommands.Add(hookCommand);

        return rootCommand;
    }

    public static Task<int> InvokeAsync(IServiceProvider provider, string[] args, CancellationToken cancellationToken = default) =>
        InvokeAsync(Create(provider), args, cancellationToken);

    internal static async Task<int> InvokeAsync(RootCommand rootCommand, string[] args, CancellationToken cancellationToken)
    {
        // Expected failures come back as results with their own exit code. This handler is for unexpected
        // exceptions only, and the default handler would exit with 1, which reads as "violations found".
        try
        {
            var parseResult = CommandLineParser.Parse(rootCommand, args, new ParserConfiguration());
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
