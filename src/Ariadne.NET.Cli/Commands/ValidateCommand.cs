using System.CommandLine;
using System.Reflection;
using System.Text;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Reports;
using Ariadne.NET.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Commands;

public static class ValidateCommand
{
    private const string TextFormat = "text";
    private const string JsonFormat = "json";
    private const string SarifFormat = "sarif";

    // A seam for tests: JSON and SARIF reach the standard output as bytes, so Console.SetOut cannot capture them.
    internal static Func<Stream> OpenStandardOutput { get; set; } = Console.OpenStandardOutput;

    public static Command Create(IServiceProvider provider)
    {
        var pathArgument = new Argument<string>("path")
        {
            Description = "Path to a .sln, .slnx, .slnf or .csproj, or a directory holding exactly one",
            DefaultValueFactory = _ => ".",
        };

        var formatOption = new Option<string>("--format")
        {
            Description = "Report format: text, json or sarif",
            DefaultValueFactory = _ => TextFormat,
        };
        formatOption.AcceptOnlyFromAmong(TextFormat, JsonFormat, SarifFormat);

        var outputOption = new Option<string?>("--output")
        {
            Description = "Write the report to this file instead of standard output; its folder is created when missing",
        };

        var command = new Command("validate", "Validate a solution or project against every pack under .architecture/")
        {
            pathArgument,
            formatOption,
            outputOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(pathArgument)!;
            var outputPath = parseResult.GetValue(outputOption);
            var validator = provider.GetRequiredService<IPackSetValidator>();
            var result = await validator.ValidateAsync(path, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return ErrorOutput.Report(result.Error);
            }

            return Emit(result.Value, RootOf(path), parseResult.GetValue(formatOption)!, outputPath);
        });

        return command;
    }

    private static int Emit(ArchitectureReport report, string folder, string format, string? outputPath)
    {
        var exitCode = report.IsValid ? ExitCodes.Success : ExitCodes.Violations;
        var isText = string.Equals(format, TextFormat, StringComparison.Ordinal);
        if (isText && outputPath is null)
        {
            return Print(Console.Out, report, folder);
        }

        if (isText)
        {
            return WriteToFile(outputPath!, w => TextReportWriter.Write(w, report, folder)) ?? exitCode;
        }

        var context = new ReportContext(ReportRoot.Find(folder), ToolVersion());
        var json = string.Equals(format, JsonFormat, StringComparison.Ordinal);
        void WriteReport(Stream stream)
        {
            if (json)
            {
                JsonReportWriter.Write(stream, report, context);
            }
            else
            {
                SarifReportWriter.Write(stream, report, context);
            }
        }

        if (outputPath is null)
        {
            // Stdout stays parseable, so the text summary and the violation detail go to stderr.
            TextReportWriter.Write(Console.Error, report, folder);
            return WriteBytes(OpenStandardOutput, "standard output", WriteReport) ?? exitCode;
        }

        var written = WriteBytes(() => CreateFile(outputPath), outputPath, WriteReport);
        if (written is not null)
        {
            return written.Value;
        }

        Print(Console.Out, report, folder);
        return exitCode;
    }

    private static Stream CreateFile(string path)
    {
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.Read);
    }

    // Returns null on success, else the exit code after reporting the failure.
    private static int? WriteBytes(Func<Stream> open, string target, Action<Stream> write)
    {
        try
        {
            using var buffer = new MemoryStream();
            write(buffer);
            using var stream = open();
            buffer.Position = 0;
            buffer.CopyTo(stream);
            stream.Flush();
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return ReportWriteFailure(target, ex);
        }
    }

    private static int? WriteToFile(string path, Action<TextWriter> write)
    {
        try
        {
            using var stream = CreateFile(path);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            write(writer);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return ReportWriteFailure(path, ex);
        }
    }

    private static int ReportWriteFailure(string target, Exception ex) =>
        ErrorOutput.Report(new AriadneError(AriadneErrorCode.Unavailable, $"The report for '{target}' could not be written.", ex.Message));

    private static string ToolVersion()
    {
        // The engine's version, as ArchitectureCheck reports it, not the CLI's own.
        var assembly = typeof(ReportRoot).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? version[..plus] : version;
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
