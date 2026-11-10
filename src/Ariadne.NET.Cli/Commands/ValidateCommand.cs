using System.CommandLine;
using Ariadne.NET.Analysis;
using Ariadne.NET.Reports;
using Ariadne.NET.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Commands;

public static class ValidateCommand
{
    public static Command Create(IServiceProvider provider)
    {
        var pathArgument = new Argument<string>("path")
        {
            Description = "Path to a .sln, .slnx, .slnf or .csproj, or a directory holding exactly one",
            DefaultValueFactory = _ => ".",
        };

        var command = new Command("validate", "Validate a solution or project against every pack under .architecture/")
        {
            pathArgument,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(pathArgument)!;
            var validator = provider.GetRequiredService<IPackSetValidator>();
            var result = await validator.ValidateAsync(path, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return ErrorOutput.Report(result.Error);
            }

            return Print(Console.Out, result.Value, RootOf(path));
        });

        return command;
    }

    private static string RootOf(string path)
    {
        var full = Path.GetFullPath(path);
        return Directory.Exists(full) ? full : Path.GetDirectoryName(full)!;
    }

    internal static int Print(TextWriter output, ArchitectureReport report, string root)
    {
        TextReportWriter.Write(output, report, root);
        return report.IsValid ? ExitCodes.Success : ExitCodes.Violations;
    }
}
