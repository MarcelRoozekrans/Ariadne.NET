using System.Reflection;
using Ariadne.NET.Analysis;
using Ariadne.NET.Reports;
using Ariadne.NET.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Testing;

/// <summary>
/// Checks a solution against every rule pack in the nearest <c>.architecture</c> folder above it, from a unit test in any test
/// framework: the same check <c>ariadne validate</c> runs.
/// </summary>
public static class ArchitectureCheck
{
    private static readonly Lazy<ServiceProvider> Services = new(BuildServices, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Checks the solution and returns the report, violations included.</summary>
    /// <param name="solutionPath">
    /// A <c>.sln</c>, <c>.slnx</c>, <c>.slnf</c> or <c>.csproj</c>, or a folder holding exactly one. When <see langword="null"/>, the
    /// single <c>.sln</c> or <c>.slnx</c> in the nearest folder at or above <see cref="AppContext.BaseDirectory"/> that has any.
    /// </param>
    /// <returns>The merged report of every pack.</returns>
    /// <exception cref="ArchitectureCheckException">The check could not run; the message says why.</exception>
    public static ArchitectureReport Run(string? solutionPath = null) =>
        Task.Run(() => RunAsync(solutionPath)).GetAwaiter().GetResult();

    /// <summary>Checks the solution and returns the report, violations included.</summary>
    /// <param name="solutionPath">The solution to check, or <see langword="null"/> to find it as <see cref="Run"/> does.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The merged report of every pack.</returns>
    /// <exception cref="ArchitectureCheckException">The check could not run; the message says why.</exception>
    public static Task<ArchitectureReport> RunAsync(string? solutionPath = null, CancellationToken cancellationToken = default) =>
        RunFromAsync(solutionPath, AppContext.BaseDirectory, cancellationToken);

    /// <summary>Checks the solution and throws when it breaks a rule.</summary>
    /// <param name="solutionPath">The solution to check, or <see langword="null"/> to find it as <see cref="Run"/> does.</param>
    /// <exception cref="ArchitectureViolationException">The solution breaks at least one rule; the message is the text report.</exception>
    /// <exception cref="ArchitectureCheckException">The check could not run; the message says why.</exception>
    public static void AssertValid(string? solutionPath = null) =>
        Task.Run(() => AssertValidAsync(solutionPath)).GetAwaiter().GetResult();

    /// <summary>Checks the solution and throws when it breaks a rule.</summary>
    /// <param name="solutionPath">The solution to check, or <see langword="null"/> to find it as <see cref="Run"/> does.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the check passed.</returns>
    /// <exception cref="ArchitectureViolationException">The solution breaks at least one rule; the message is the text report.</exception>
    /// <exception cref="ArchitectureCheckException">The check could not run; the message says why.</exception>
    public static Task AssertValidAsync(string? solutionPath = null, CancellationToken cancellationToken = default) =>
        AssertValidFromAsync(solutionPath, AppContext.BaseDirectory, cancellationToken);

    /// <summary>Checks the solution, writes the report to a file, then throws when the solution breaks a rule.</summary>
    /// <param name="solutionPath">The solution to check, or <see langword="null"/> to find it as <see cref="Run"/> does.</param>
    /// <param name="reportPath">The file to write. Its folder is created when missing and an existing file is overwritten.</param>
    /// <param name="format">The format of the report.</param>
    /// <remarks>The file is written before the exception is thrown, so a failing test still leaves its report behind.</remarks>
    /// <exception cref="ArchitectureViolationException">The solution breaks at least one rule; the message is the text report.</exception>
    /// <exception cref="ArchitectureCheckException">The check could not run; the message says why.</exception>
    public static void AssertValid(string? solutionPath, string reportPath, ReportFormat format) =>
        Task.Run(() => AssertValidAsync(solutionPath, reportPath, format)).GetAwaiter().GetResult();

    /// <summary>Checks the solution, writes the report to a file, then throws when the solution breaks a rule.</summary>
    /// <param name="solutionPath">The solution to check, or <see langword="null"/> to find it as <see cref="Run"/> does.</param>
    /// <param name="reportPath">The file to write. Its folder is created when missing and an existing file is overwritten.</param>
    /// <param name="format">The format of the report.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the check passed.</returns>
    /// <remarks>The file is written before the exception is thrown, so a failing test still leaves its report behind.</remarks>
    /// <exception cref="ArchitectureViolationException">The solution breaks at least one rule; the message is the text report.</exception>
    /// <exception cref="ArchitectureCheckException">The check could not run; the message says why.</exception>
    public static Task AssertValidAsync(string? solutionPath, string reportPath, ReportFormat format, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reportPath);
        return AssertValidFromAsync(solutionPath, AppContext.BaseDirectory, cancellationToken, (reportPath, format));
    }

    /// <summary>Writes a report returned by <see cref="Run"/> to a file.</summary>
    /// <param name="report">The report to write.</param>
    /// <param name="format">The format of the file.</param>
    /// <param name="path">The file to write. Its folder is created when missing and an existing file is overwritten.</param>
    /// <remarks>
    /// A report does not record where the solution was, so file paths in it are made relative to the repository root found from
    /// <see cref="AppContext.BaseDirectory"/>, the output folder of the test project: the nearest ancestor with a <c>.git</c>
    /// entry, else that folder itself. <see cref="AssertValid(string?, string, ReportFormat)"/> knows the solution it checked and
    /// uses the repository root of that solution instead. Use that overload when the solution is outside the repository of the
    /// test project.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is not a defined <see cref="ReportFormat"/>.</exception>
    public static void WriteReport(ArchitectureReport report, ReportFormat format, string path) =>
        WriteReport(report, format, path, ReportRoot.Find(AppContext.BaseDirectory));

    internal static void WriteReport(ArchitectureReport report, ReportFormat format, string path, string root)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var context = new ReportContext(root, ToolVersion());
        using var buffer = new MemoryStream();
        switch (format)
        {
            case ReportFormat.Json:
                JsonReportWriter.Write(buffer, report, context);
                break;
            case ReportFormat.Sarif:
                SarifReportWriter.Write(buffer, report, context);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "The report format is not defined.");
        }

        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(full, buffer.ToArray());
    }

    /// <summary>
    /// <see cref="RunAsync"/> with the folder the solution search starts in when <paramref name="solutionPath"/> is
    /// <see langword="null"/>. A seam for tests, whose output folder may not lie below the solution.
    /// </summary>
    internal static async Task<ArchitectureReport> RunFromAsync(string? solutionPath, string startFolder, CancellationToken cancellationToken)
    {
        var (report, _) = await CheckAsync(solutionPath, startFolder, cancellationToken).ConfigureAwait(false);
        return report;
    }

    /// <summary><see cref="AssertValidAsync"/> with the folder the solution search starts in; see <see cref="RunFromAsync"/>.</summary>
    internal static async Task AssertValidFromAsync(
        string? solutionPath, string startFolder, CancellationToken cancellationToken, (string Path, ReportFormat Format)? reportFile = null)
    {
        var (report, root) = await CheckAsync(solutionPath, startFolder, cancellationToken).ConfigureAwait(false);
        if (reportFile is { } file)
        {
            WriteReport(report, file.Format, file.Path, ReportRoot.Find(root));
        }

        if (!report.IsValid)
        {
            throw new ArchitectureViolationException(report, root);
        }
    }

    private static async Task<(ArchitectureReport Report, string Root)> CheckAsync(
        string? solutionPath, string startFolder, CancellationToken cancellationToken)
    {
        var path = solutionPath ?? Locate(startFolder);
        var validator = Services.Value.GetRequiredService<IPackSetValidator>();
        var result = await validator.ValidateAsync(path, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new ArchitectureCheckException(result.Error);
        }

        return (result.Value, RootOf(path));
    }

    private static string Locate(string startFolder)
    {
        var found = SolutionLocator.Find(startFolder);
        return found.IsSuccess ? found.Value : throw new ArchitectureCheckException(found.Error);
    }

    // The folder violation paths are shown relative to, as ariadne validate shows them: the path itself when it is a folder,
    // else the folder that holds the solution or project.
    private static string RootOf(string path)
    {
        var full = Path.GetFullPath(path);
        return Directory.Exists(full) ? full : Path.GetDirectoryName(full)!;
    }

    // The engine's version, as ariadne validate reports it, without build metadata.
    private static string ToolVersion()
    {
        var assembly = typeof(ReportRoot).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? version[..plus] : version;
    }

    // MSBuildLocator is registered by the solution loader on its first load, as for the CLI, so nothing MSBuild-related happens here.
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        services.AddAriadneNETWorkspaces();
        return services.BuildServiceProvider();
    }
}
