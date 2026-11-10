using Ariadne.NET.Analysis;
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
    internal static async Task AssertValidFromAsync(string? solutionPath, string startFolder, CancellationToken cancellationToken)
    {
        var (report, root) = await CheckAsync(solutionPath, startFolder, cancellationToken).ConfigureAwait(false);
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

    // MSBuildLocator is registered by the solution loader on its first load, as for the CLI, so nothing MSBuild-related happens here.
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        services.AddAriadneNETWorkspaces();
        return services.BuildServiceProvider();
    }
}
