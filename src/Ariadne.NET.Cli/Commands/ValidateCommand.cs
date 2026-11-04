using System.CommandLine;
using Ariadne.NET.Analysis;
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

        var command = new Command("validate", "Validate a solution or project against architecture rules")
        {
            pathArgument,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(pathArgument)!;
            var resolved = RulesCommand.ResolveRulesFromFile(provider);
            if (resolved.IsFailure)
            {
                return ErrorOutput.Report(resolved.Error);
            }

            var validator = provider.GetRequiredService<ISolutionValidator>();
            var result = await validator.ValidateAsync(resolved.Value, path, cancellationToken).ConfigureAwait(false);
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
        output.WriteLine($"Checked {report.TypesChecked} types in {report.Projects.Count} projects");
        foreach (var skipped in report.SkippedProjects)
        {
            output.WriteLine($"Skipped {skipped.Name}: {skipped.Reason}");
        }

        if (report.IsValid)
        {
            output.WriteLine("Result: PASS - No violations found.");
            return ExitCodes.Success;
        }

        output.WriteLine($"Result: FAIL - {report.Violations.Count} violation(s) found.");
        output.WriteLine();
        foreach (var violation in report.Violations)
        {
            var where = violation.Location is { } location
                ? $"{Path.GetRelativePath(root, location.FilePath).Replace('\\', '/')}:{location.Line}"
                : violation.SourceType;
            output.WriteLine($"  [{violation.Kind}] {where}");
            output.WriteLine($"    {violation.Message}");
        }

        return ExitCodes.Violations;
    }
}
