using System.Collections.Frozen;
using System.Globalization;
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
    private const string RestoreHint = "run 'dotnet restore' first";
    private const string AssetsFileName = "project.assets.json";
    private const string NotCSharp = "not a C# project";
    private const int MaxListedDiagnostics = 5;

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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(rootDirectory);

        return BuildAsync(solution, rootDirectory, named: null, owner: null, cancellationToken);
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

            if (snapshot.Length > 0)
            {
                return Failure(LoadFailure(snapshot));
            }

            var result = await BuildAsync(solution, Path.GetDirectoryName(file)!, named, workspace, cancellationToken)
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

    // The steps shared by a real load and the AdhocWorkspace seam, after any workspace failure has ended a real load: pick the named
    // projects, check they are restored and loaded, skip non-C# projects, then compile each C# project and fail on any compile error.
    private static async Task<Result<LoadedSolution, AriadneError>> BuildAsync(
        Solution solution,
        string rootDirectory,
        IReadOnlySet<string>? named,
        IDisposable? owner,
        CancellationToken cancellationToken)
    {
        var selected = SelectNamed(solution, named);

        if (CheckRestored(selected, rootDirectory) is { } notRestored)
        {
            return Failure(notRestored);
        }

        if (MissingNamed(selected, named, rootDirectory) is { } missing)
        {
            return Failure(missing);
        }

        var (csharp, skipped) = Partition(selected);
        if (csharp.Count == 0)
        {
            return Failure(
                AriadneErrorCode.Invalid,
                "The solution holds no C# project to check.",
                skipped.Count == 0 ? null : string.Join('\n', skipped.Select(s => $"{s.Name}: {s.Reason}")));
        }

        if (DuplicateName(csharp) is { } duplicate)
        {
            return Failure(duplicate);
        }

        var analyzed = new List<AnalyzedProject>(csharp.Count);
        for (var i = 0; i < csharp.Count; i++)
        {
            var compiled = await CompileAsync(csharp[i], rootDirectory, cancellationToken).ConfigureAwait(false);
            if (compiled.IsFailure)
            {
                return Failure(compiled.Error);
            }

            analyzed.Add(compiled.Value);
        }

        return Result<LoadedSolution, AriadneError>.Success(new LoadedSolution(rootDirectory, analyzed, skipped, owner));
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

    // Non-C# projects are skipped, once per project even when it has several target frameworks.
    private static (List<Project> CSharp, List<SkippedProject> Skipped) Partition(List<Project> projects)
    {
        var csharp = new List<Project>(projects.Count);
        var skipped = new List<SkippedProject>();
        foreach (ref readonly var project in CollectionsMarshal.AsSpan(projects))
        {
            if (string.Equals(project.Language, LanguageNames.CSharp, StringComparison.Ordinal))
            {
                csharp.Add(project);
                continue;
            }

            var name = SplitName(project.Name).Name;
            if (!skipped.Exists(s => string.Equals(s.Name, name, StringComparison.Ordinal)))
            {
                skipped.Add(new SkippedProject(name, NotCSharp));
            }
        }

        return (csharp, skipped);
    }

    // Ariadne never restores. An unrestored project loads without a workspace failure and only shows compile errors, so this runs first.
    private static AriadneError? CheckRestored(List<Project> projects, string rootDirectory)
    {
        var names = new List<string>();
        var lines = new List<string>();
        var seen = new HashSet<string>(NamedProjects.PathComparer);
        foreach (ref readonly var project in CollectionsMarshal.AsSpan(projects))
        {
            if (project.FilePath is not { Length: > 0 } filePath || !seen.Add(filePath))
            {
                continue;
            }

            var assets = Path.Combine(Path.GetDirectoryName(filePath)!, "obj", AssetsFileName);
            if (!File.Exists(assets))
            {
                names.Add($"'{SplitName(project.Name).Name}'");
                lines.Add($"{Relative(rootDirectory, filePath)}: obj/{AssetsFileName} is missing");
            }
        }

        if (names.Count == 0)
        {
            return null;
        }

        var subject = names.Count == 1 ? $"Project {names[0]} is" : $"Projects {string.Join(", ", names)} are";
        return new AriadneError(AriadneErrorCode.Invalid, $"{subject} not restored; {RestoreHint}.", string.Join('\n', lines));
    }

    // A workspace diagnostic carries only a message; MSBuildWorkspace puts the project file's path in it.
    private static AriadneError LoadFailure(IReadOnlyList<string> failures)
    {
        var match = ProjectFilePattern().Match(failures[0]);
        var subject = match.Success
            ? $"Project '{Path.GetFileNameWithoutExtension(match.Value)}' did not load"
            : "The solution did not load completely";
        var hint = Mentions(failures, AssetsFileName) ? $"; {RestoreHint}" : "";
        return new AriadneError(AriadneErrorCode.Invalid, $"{subject}{hint}.", string.Join('\n', failures));
    }

    // A project the solution, solution filter or project file names but the workspace did not load.
    private static AriadneError? MissingNamed(List<Project> selected, IReadOnlySet<string>? named, string rootDirectory)
    {
        if (named is null)
        {
            return null;
        }

        var loaded = new HashSet<string>(NamedProjects.PathComparer);
        foreach (ref readonly var project in CollectionsMarshal.AsSpan(selected))
        {
            loaded.Add(NamedProjects.Normalize(project.FilePath!));
        }

        foreach (var path in named.Order(NamedProjects.PathComparer))
        {
            if (!loaded.Contains(path))
            {
                return new AriadneError(
                    AriadneErrorCode.Invalid,
                    $"Project '{Path.GetFileNameWithoutExtension(path)}' did not load.",
                    $"{Relative(rootDirectory, path)}: the workspace has no project for this file");
            }
        }

        return null;
    }

    // The engine merges entries by project name, so two projects of one name would be checked as one.
    private static AriadneError? DuplicateName(List<Project> projects)
    {
        var frameworks = new Dictionary<string, HashSet<string?>>(StringComparer.Ordinal);
        foreach (ref readonly var project in CollectionsMarshal.AsSpan(projects))
        {
            var (name, targetFramework) = SplitName(project.Name);
            if (!frameworks.TryGetValue(name, out var seen))
            {
                frameworks.Add(name, seen = []);
            }
            else if (seen.Contains(targetFramework) || (targetFramework is null) != seen.Contains(null))
            {
                return new AriadneError(AriadneErrorCode.Invalid, $"More than one project is named '{name}'.");
            }

            seen.Add(targetFramework);
        }

        return null;
    }

    private static async Task<Result<AnalyzedProject, AriadneError>> CompileAsync(
        Project project,
        string rootDirectory,
        CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation is null)
        {
            return Result<AnalyzedProject, AriadneError>.Failure(
                new AriadneError(AriadneErrorCode.Invalid, $"Project '{project.Name}' has no compilation."));
        }

        var errorCount = 0;
        var lines = new List<string>(MaxListedDiagnostics);
        var mentionsAssets = false;
        foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken))
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error)
            {
                continue;
            }

            errorCount++;
            var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
            mentionsAssets |= message.Contains(AssetsFileName, StringComparison.OrdinalIgnoreCase);
            if (lines.Count < MaxListedDiagnostics)
            {
                lines.Add(Describe(diagnostic, message, project.Name, rootDirectory));
            }
        }

        if (errorCount > 0)
        {
            var errors = errorCount == 1 ? "1 compile error" : string.Create(CultureInfo.InvariantCulture, $"{errorCount} compile errors");
            var hint = mentionsAssets ? $"; {RestoreHint}" : "";
            return Result<AnalyzedProject, AriadneError>.Failure(
                new AriadneError(AriadneErrorCode.Invalid, $"Project '{project.Name}' has {errors}{hint}.", string.Join('\n', lines)));
        }

        var generatedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var document in await project.GetSourceGeneratedDocumentsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (document.FilePath is { Length: > 0 } filePath)
            {
                generatedPaths.Add(filePath);
            }
        }

        var generatedTrees = compilation.SyntaxTrees.Where(t => generatedPaths.Contains(t.FilePath)).ToFrozenSet();
        var (name, targetFramework) = SplitName(project.Name);
        return Result<AnalyzedProject, AriadneError>.Success(
            new AnalyzedProject(name, targetFramework, compilation) { GeneratedTrees = generatedTrees });
    }

    // path:line:column: id message, with the path relative to the root; a diagnostic without a source location uses the project name.
    private static string Describe(Diagnostic diagnostic, string message, string projectName, string rootDirectory)
    {
        var span = diagnostic.Location.GetLineSpan();
        if (!diagnostic.Location.IsInSource || string.IsNullOrEmpty(span.Path))
        {
            return $"{projectName}: {diagnostic.Id} {message}";
        }

        var start = span.StartLinePosition;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Relative(rootDirectory, span.Path)}:{start.Line + 1}:{start.Character + 1}: {diagnostic.Id} {message}");
    }

    // MSBuildWorkspace names each target framework of a multi-targeting project "Name(tfm)".
    private static (string Name, string? TargetFramework) SplitName(string projectName)
    {
        if (projectName.EndsWith(')'))
        {
            var open = projectName.LastIndexOf('(');
            if (open > 0 && open < projectName.Length - 2)
            {
                return (projectName[..open], projectName[(open + 1)..^1]);
            }
        }

        return (projectName, null);
    }

    private static bool Mentions(IReadOnlyList<string> messages, string text)
    {
        foreach (var message in messages)
        {
            if (message.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Relative(string rootDirectory, string path) =>
        Path.IsPathRooted(path) ? Path.GetRelativePath(rootDirectory, path) : path;

    [GeneratedRegex(@"[^\s'""]+\.[A-Za-z]+proj\b", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ProjectFilePattern();

    private static Result<LoadedSolution, AriadneError> Failure(AriadneError error) =>
        Result<LoadedSolution, AriadneError>.Failure(error);

    private static Result<LoadedSolution, AriadneError> Failure(AriadneErrorCode code, string message, string? detail = null) =>
        Result<LoadedSolution, AriadneError>.Failure(new AriadneError(code, message, detail));
}
