using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces;

/// <summary>Loads solutions and projects with MSBuildWorkspace and fails on any load that is not complete.</summary>
public sealed partial class SolutionLoader : ISolutionLoader
{
    private const string NotCSharp = "not a C# project";

    /// <inheritdoc />
    public async Task<Result<LoadedSolution, AriadneError>> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var resolved = SolutionPath.Resolve(path);
        if (resolved.IsFailure)
        {
            return Failure(resolved.Error);
        }

        var file = resolved.Value;
        var named = await NamedProjects.ReadAsync(file, cancellationToken).ConfigureAwait(false);
        if (named.IsFailure)
        {
            return Failure(named.Error);
        }

        if (MSBuildLocatorRegistration.EnsureRegistered() is { } unavailable)
        {
            return Failure(unavailable);
        }

        return await OpenAsync(file, named.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds the loaded solution from an already opened <paramref name="solution"/>, treating every project in it as named.</summary>
    /// <remarks>The caller keeps ownership of the workspace; disposing the result does not dispose it.</remarks>
    internal static Task<Result<LoadedSolution, AriadneError>> FromSolutionAsync(
        Solution solution,
        string rootDirectory,
        CancellationToken cancellationToken) =>
        FromSolutionAsync(solution, rootDirectory, failures: [], cancellationToken);

    /// <summary>As <see cref="FromSolutionAsync(Solution, string, CancellationToken)"/>, with the workspace failures a load reported.</summary>
    internal static Task<Result<LoadedSolution, AriadneError>> FromSolutionAsync(
        Solution solution,
        string rootDirectory,
        IReadOnlyList<string> failures,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(rootDirectory);
        ArgumentNullException.ThrowIfNull(failures);

        return BuildAsync(solution, rootDirectory, named: null, failures, owner: null, cancellationToken);
    }

    // The first method that touches MSBuildWorkspace; kept out of line so the JIT loads no MSBuild type before the locator registers.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<Result<LoadedSolution, AriadneError>> OpenAsync(
        string file,
        IReadOnlySet<string> named,
        CancellationToken cancellationToken)
    {
        MSBuildWorkspace? workspace = MSBuildWorkspace.Create();
        try
        {
            var failures = new List<string>();
            Solution solution;
            using (workspace.RegisterWorkspaceFailedHandler(e => AddFailure(failures, e.Diagnostic)))
            {
                try
                {
                    solution = string.Equals(Path.GetExtension(file), ".csproj", StringComparison.OrdinalIgnoreCase)
                        ? (await workspace.OpenProjectAsync(file, progress: null, cancellationToken).ConfigureAwait(false)).Solution
                        : await workspace.OpenSolutionAsync(file, progress: null, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException
                    or FormatException or XmlException)
                {
                    return Failure(AriadneErrorCode.Invalid, $"'{file}' could not be opened.", $"{file}: {ex.Message}");
                }
            }

            string[] snapshot;
            lock (failures)
            {
                snapshot = [.. failures];
            }

            var result = await BuildAsync(solution, Path.GetDirectoryName(file)!, named, snapshot, workspace, cancellationToken)
                .ConfigureAwait(false);
            if (result.IsSuccess)
            {
                workspace = null;
            }

            return result;
        }
        finally
        {
            workspace?.Dispose();
        }
    }

    private static void AddFailure(List<string> failures, WorkspaceDiagnostic diagnostic)
    {
        if (diagnostic.Kind != WorkspaceDiagnosticKind.Failure)
        {
            return;
        }

        lock (failures)
        {
            failures.Add(diagnostic.Message);
        }
    }

    // The steps shared by a real load and the AdhocWorkspace seam:
    // 1. pick the named projects the workspace loaded;
    // 2. skip named project files it did not load because they are not C#, such as .dcproj or .shproj, with their failures;
    // 3. fail on a workspace failure about no project, then on a named .csproj that did not load;
    // 4. skip the loaded non-C# projects, then compile each C# project and fail on what makes it uncheckable.
    // MSBuildWorkspace reports an MSBuild warning as a Failure too, so a failure about a project only fails the load when that
    // project is not in the workspace, named or referenced, was left without references or documents, or does not compile. The first two fail with the project's workspace failures; a project that does not compile fails with its compile
    // error, followed by its workspace failures. A project that compiles is checked whatever its failures said (#117).
    // Restore is detected from each compilation, after the failures, so a malformed project file is not reported as unrestored.
    private static async Task<Result<LoadedSolution, AriadneError>> BuildAsync(
        Solution solution,
        string rootDirectory,
        IReadOnlySet<string>? named,
        IReadOnlyList<string> failures,
        IDisposable? owner,
        CancellationToken cancellationToken)
    {
        var selected = SelectNamed(solution, named);
        var (unloadedCSharp, unloadedOther) = Unloaded(selected, named);

        var (byProject, general) = BySubject(WithoutFailuresOf(failures, unloadedOther));
        if (general.Count > 0)
        {
            return Failure(LoadFailure(general));
        }

        if (unloadedCSharp.Count > 0)
        {
            var path = unloadedCSharp[0];
            if (byProject.TryGetValue(NamedProjects.Normalize(path), out var failed))
            {
                return Failure(LoadFailure(failed));
            }

            return Failure(
                AriadneErrorCode.Invalid,
                $"Project '{Path.GetFileNameWithoutExtension(path)}' did not load.",
                $"{ProjectCompiler.Relative(rootDirectory, path)}: the workspace has no project for this file");
        }

        if (FailuresOfMissingProjects(solution, byProject) is { Count: > 0 } missing)
        {
            return Failure(LoadFailure(missing));
        }

        var (csharp, skipped) = Partition(selected, unloadedOther);
        if (csharp.Count == 0)
        {
            return Failure(
                AriadneErrorCode.Invalid,
                "The solution holds no C# project to check.",
                skipped.Count == 0 ? null : string.Join('\n', skipped.Select(s => $"{s.Name}: {s.Reason}")));
        }

        if (DuplicateName(csharp, rootDirectory) is { } duplicate)
        {
            return Failure(duplicate);
        }

        var analyzed = await CompileAsync(csharp, rootDirectory, byProject, cancellationToken).ConfigureAwait(false);
        if (analyzed.IsFailure)
        {
            return Failure(analyzed.Error);
        }

        return Result<LoadedSolution, AriadneError>.Success(new LoadedSolution(rootDirectory, analyzed.Value, skipped, owner));
    }

    // Compiles each C# project. A project with workspace failures and no references or no documents did not load: its evaluation
    // failed, which leaves an empty project, or its design-time build stopped before the compiler arguments. Its failures are the
    // error. Otherwise a compile error is the error, with the project's failures after it.
    private static async Task<Result<List<AnalyzedProject>, AriadneError>> CompileAsync(
        List<Project> csharp,
        string rootDirectory,
        Dictionary<string, List<string>> byProject,
        CancellationToken cancellationToken)
    {
        var analyzed = new List<AnalyzedProject>(csharp.Count);
        for (var i = 0; i < csharp.Count; i++)
        {
            var failed = csharp[i].FilePath is { Length: > 0 } filePath
                && byProject.TryGetValue(NamedProjects.Normalize(filePath), out var list)
                ? list
                : null;
            if (failed is not null && (csharp[i].MetadataReferences.Count == 0 || csharp[i].DocumentIds.Count == 0))
            {
                return Result<List<AnalyzedProject>, AriadneError>.Failure(LoadFailure(failed));
            }

            var compiled = await ProjectCompiler.CompileAsync(csharp[i], rootDirectory, cancellationToken).ConfigureAwait(false);
            if (compiled.IsFailure)
            {
                return Result<List<AnalyzedProject>, AriadneError>.Failure(
                    failed is null ? compiled.Error : WithWorkspaceFailures(compiled.Error, failed));
            }

            analyzed.Add(compiled.Value);
        }

        return Result<List<AnalyzedProject>, AriadneError>.Success(analyzed);
    }

    // The workspace also holds the projects a named project references; they compile as references only. Null names every project.
    private static List<Project> SelectNamed(Solution solution, IReadOnlySet<string>? named)
    {
        var selected = new List<Project>();
        foreach (var project in solution.Projects)
        {
            if (named is null || (project.FilePath is { Length: > 0 } filePath && named.Contains(NamedProjects.Normalize(filePath))))
            {
                selected.Add(project);
            }
        }

        selected.Sort(static (x, y) => string.CompareOrdinal(x.Name, y.Name));
        return selected;
    }

    // The named project files with no loaded project: .csproj files, which must load, and the rest, which are not C# and are skipped.
    private static (List<string> CSharp, List<string> Other) Unloaded(List<Project> selected, IReadOnlySet<string>? named)
    {
        var csharp = new List<string>();
        var other = new List<string>();
        if (named is null)
        {
            return (csharp, other);
        }

        var loaded = new HashSet<string>(NamedProjects.PathComparer);
        foreach (ref readonly var project in CollectionsMarshal.AsSpan(selected))
        {
            loaded.Add(NamedProjects.Normalize(project.FilePath!));
        }

        foreach (var path in named.Order(NamedProjects.PathComparer))
        {
            if (loaded.Contains(path))
            {
                continue;
            }

            (string.Equals(Path.GetExtension(path), ".csproj", StringComparison.OrdinalIgnoreCase) ? csharp : other).Add(path);
        }

        return (csharp, other);
    }

    /// <summary>
    /// Drops the failures whose subject is a skipped project file. The subject is the first quoted path in the message, where
    /// MSBuildWorkspace puts the project it failed on; a skipped file mentioned later does not drop a failure about another project.
    /// </summary>
    internal static List<string> WithoutFailuresOf(IReadOnlyList<string> failures, IReadOnlyList<string> skippedPaths)
    {
        var skipped = new HashSet<string>(skippedPaths.Count, NamedProjects.PathComparer);
        foreach (var path in skippedPaths)
        {
            skipped.Add(NamedProjects.Normalize(path));
        }

        var remaining = new List<string>(failures.Count);
        foreach (var failure in failures)
        {
            if (SubjectPath(failure) is not { } subject || !skipped.Contains(NamedProjects.Normalize(subject)))
            {
                remaining.Add(failure);
            }
        }

        return remaining;
    }

    /// <summary>
    /// Splits workspace failures into those about a project file, keyed by its normalized path, and the rest. The project is the
    /// subject path of the message when it ends in <c>proj</c>.
    /// </summary>
    internal static (Dictionary<string, List<string>> ByProject, List<string> General) BySubject(IReadOnlyList<string> failures)
    {
        var byProject = new Dictionary<string, List<string>>(NamedProjects.PathComparer);
        var general = new List<string>();
        foreach (var failure in failures)
        {
            if (SubjectPath(failure) is { } subject && Path.GetExtension(subject).EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            {
                var key = NamedProjects.Normalize(subject);
                if (!byProject.TryGetValue(key, out var list))
                {
                    byProject.Add(key, list = []);
                }

                list.Add(failure);
            }
            else
            {
                general.Add(failure);
            }
        }

        return (byProject, general);
    }

    // A compile error keeps its message; the project's workspace failures follow its detail, since they may say why it is incomplete.
    private static AriadneError WithWorkspaceFailures(AriadneError error, List<string> failures)
    {
        var listed = string.Join('\n', failures);
        return error with { Detail = error.Detail is { Length: > 0 } detail ? $"{detail}\n{listed}" : listed };
    }

    // The failures about project files the workspace holds no project for, such as a referenced project that did not load.
    private static List<string> FailuresOfMissingProjects(Solution solution, Dictionary<string, List<string>> byProject)
    {
        var loaded = new HashSet<string>(NamedProjects.PathComparer);
        foreach (var project in solution.Projects)
        {
            if (project.FilePath is { Length: > 0 } filePath)
            {
                loaded.Add(NamedProjects.Normalize(filePath));
            }
        }

        var missing = new List<string>();
        foreach (var (path, failed) in byProject)
        {
            if (!loaded.Contains(path))
            {
                missing.AddRange(failed);
            }
        }

        return missing;
    }

    // The first quoted, rooted path in a workspace diagnostic, which may contain spaces and apostrophes. A quoted path that ends in a
    // project extension is taken whole, so an apostrophe inside it does not cut it short; otherwise the first quoted text is used.
    private static string? SubjectPath(string message)
    {
        var match = QuotedProjectPath().Match(message);
        if (!match.Success)
        {
            match = QuotedText().Match(message);
        }

        return match.Success && Path.IsPathRooted(match.Groups["path"].Value) ? match.Groups["path"].Value : null;
    }

    // Non-C# projects are skipped, once per project even when it has several target frameworks, with the named files that did not load.
    private static (List<Project> CSharp, List<SkippedProject> Skipped) Partition(List<Project> projects, List<string> unloadedOther)
    {
        var csharp = new List<Project>(projects.Count);
        var skipped = new List<SkippedProject>();
        foreach (ref readonly var project in CollectionsMarshal.AsSpan(projects))
        {
            if (string.Equals(project.Language, LanguageNames.CSharp, StringComparison.Ordinal))
            {
                csharp.Add(project);
            }
            else
            {
                AddSkipped(skipped, ProjectName.Split(project).Name);
            }
        }

        foreach (ref readonly var path in CollectionsMarshal.AsSpan(unloadedOther))
        {
            AddSkipped(skipped, Path.GetFileNameWithoutExtension(path));
        }

        return (csharp, skipped);
    }

    private static void AddSkipped(List<SkippedProject> skipped, string name)
    {
        if (!skipped.Exists(s => string.Equals(s.Name, name, StringComparison.Ordinal)))
        {
            skipped.Add(new SkippedProject(name, NotCSharp));
        }
    }

    // A workspace diagnostic carries only a message; MSBuildWorkspace puts the project file's path in it.
    private static AriadneError LoadFailure(List<string> failures)
    {
        var path = SubjectPath(failures[0]);
        var subject = path is not null && Path.GetExtension(path).EndsWith("proj", StringComparison.OrdinalIgnoreCase)
            ? $"Project '{Path.GetFileNameWithoutExtension(path)}' did not load"
            : "The solution did not load completely";
        var hint = failures.Exists(f => f.Contains(ProjectCompiler.AssetsFileName, StringComparison.OrdinalIgnoreCase))
            ? ProjectCompiler.RestoreSuffix
            : "";
        return new AriadneError(AriadneErrorCode.Invalid, $"{subject}{hint}.", string.Join('\n', failures));
    }

    // The engine merges entries by project name, so two projects of one name would be checked as one.
    private static AriadneError? DuplicateName(List<Project> projects, string rootDirectory)
    {
        var frameworks = new Dictionary<string, HashSet<string?>>(StringComparer.Ordinal);
        foreach (ref readonly var project in CollectionsMarshal.AsSpan(projects))
        {
            var (name, targetFramework) = ProjectName.Split(project);
            if (!frameworks.TryGetValue(name, out var seen))
            {
                frameworks.Add(name, seen = []);
            }
            else if (seen.Contains(targetFramework) || (targetFramework is null) != seen.Contains(null))
            {
                return new AriadneError(
                    AriadneErrorCode.Invalid,
                    $"More than one project is named '{name}'.",
                    string.Join('\n', ProjectFilesNamed(projects, name, rootDirectory)));
            }

            seen.Add(targetFramework);
        }

        return null;
    }

    private static List<string> ProjectFilesNamed(List<Project> projects, string name, string rootDirectory)
    {
        var files = new List<string>();
        foreach (ref readonly var project in CollectionsMarshal.AsSpan(projects))
        {
            if (!string.Equals(ProjectName.Split(project).Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            var file = project.FilePath is { Length: > 0 } filePath
                ? $"{ProjectCompiler.Relative(rootDirectory, filePath)}: project '{project.Name}'"
                : $"{project.Name}: project without a file";
            if (!files.Contains(file, StringComparer.Ordinal))
            {
                files.Add(file);
            }
        }

        return files;
    }

    [GeneratedRegex("'(?<path>[^']+)'", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QuotedText();

    [GeneratedRegex(@"'(?<path>[^\r\n]+?\.[A-Za-z]*proj)'", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QuotedProjectPath();

    private static Result<LoadedSolution, AriadneError> Failure(AriadneError error) =>
        Result<LoadedSolution, AriadneError>.Failure(error);

    private static Result<LoadedSolution, AriadneError> Failure(AriadneErrorCode code, string message, string? detail = null) =>
        Result<LoadedSolution, AriadneError>.Failure(new AriadneError(code, message, detail));
}
