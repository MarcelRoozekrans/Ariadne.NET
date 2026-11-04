using System.Collections.Frozen;
using System.Globalization;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Microsoft.CodeAnalysis;
using ZeroAlloc.Results;

namespace Ariadne.NET.Workspaces;

/// <summary>Compiles one named C# project and turns it into an <see cref="AnalyzedProject"/>, or the reason it cannot be checked.</summary>
internal static class ProjectCompiler
{
    /// <summary>The hint an error carries when a missing restore may be its cause.</summary>
    public const string RestoreHint = "run 'dotnet restore' first";

    /// <summary>The message ending an error gets when a missing restore may be its cause.</summary>
    public const string RestoreSuffix = "; it may not be restored: " + RestoreHint;

    /// <summary>The file a restore writes, which some MSBuild and compiler messages name.</summary>
    public const string AssetsFileName = "project.assets.json";

    private const int MaxListedDiagnostics = 5;

    /// <summary>
    /// Fails on a source file that does not exist, a missing compilation, a compilation that cannot resolve <c>System.Object</c>,
    /// and any compile error, listing the first five. The restore state is not itself checked: a project that compiles completely
    /// passes even if it was never restored. A failing project whose assets file cannot be found gets the restore hint.
    /// </summary>
    public static async Task<Result<AnalyzedProject, AriadneError>> CompileAsync(
        Project project,
        string rootDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rootDirectory);

        if (MissingSourceFiles(project, rootDirectory) is { } missing)
        {
            return Failure(missing);
        }

        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation is null)
        {
            return Failure(new AriadneError(AriadneErrorCode.Invalid, $"Project '{project.Name}' has no compilation."));
        }

        if (WithoutCoreLibrary(project, compilation, rootDirectory) is { } incomplete)
        {
            return Failure(incomplete);
        }

        if (CompileErrors(project, compilation, rootDirectory, cancellationToken) is { } errors)
        {
            return Failure(errors);
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
        var (name, targetFramework) = ProjectName.Split(project);
        return Result<AnalyzedProject, AriadneError>.Success(
            new AnalyzedProject(name, targetFramework, compilation) { GeneratedTrees = generatedTrees });
    }

    // MSBuildWorkspace keeps a Compile item whose file is gone as a document with empty text, so it compiles clean where a build fails.
    private static AriadneError? MissingSourceFiles(Project project, string rootDirectory)
    {
        var lines = new List<string>();
        foreach (var document in project.Documents)
        {
            if (document.FilePath is { Length: > 0 } filePath && !File.Exists(filePath))
            {
                lines.Add($"{Relative(rootDirectory, filePath)}: file not found");
            }
        }

        if (lines.Count == 0)
        {
            return null;
        }

        var subject = lines.Count == 1
            ? "a source file that does not exist"
            : string.Create(CultureInfo.InvariantCulture, $"{lines.Count} source files that do not exist");
        return new AriadneError(AriadneErrorCode.Invalid, $"Project '{project.Name}' lists {subject}.", string.Join('\n', lines));
    }

    // Checked before the compile errors, so a compilation without the framework is not reported as dozens of CS0518 errors.
    private static AriadneError? WithoutCoreLibrary(Project project, Compilation compilation, string rootDirectory)
    {
        if (compilation.GetSpecialType(SpecialType.System_Object).TypeKind != TypeKind.Error)
        {
            return null;
        }

        var subject = project.FilePath is { Length: > 0 } filePath ? Relative(rootDirectory, filePath) : project.Name;
        var lines = new List<string> { $"{subject}: System.Object cannot be resolved" };
        return WithHint(project, $"Project '{project.Name}' cannot resolve System.Object", lines, mentionsAssets: false);
    }

    private static AriadneError? CompileErrors(
        Project project,
        Compilation compilation,
        string rootDirectory,
        CancellationToken cancellationToken)
    {
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

        if (errorCount == 0)
        {
            return null;
        }

        var errors = errorCount == 1 ? "1 compile error" : string.Create(CultureInfo.InvariantCulture, $"{errorCount} compile errors");
        return WithHint(project, $"Project '{project.Name}' has {errors}", lines, mentionsAssets);
    }

    // Adds the restore hint when an error names the assets file, or when the project is on disk and no assets file can be found.
    // The lookup is a heuristic that only adds a hint to a project that fails anyway; it never fails a project by itself.
    private static AriadneError WithHint(Project project, string subject, List<string> lines, bool mentionsAssets)
    {
        if (mentionsAssets || (project.FilePath is { Length: > 0 } && !HasAssetsFile(project)))
        {
            lines.Add(RestoreHint);
            return new AriadneError(AriadneErrorCode.Invalid, $"{subject}{RestoreSuffix}.", string.Join('\n', lines));
        }

        return new AriadneError(AriadneErrorCode.Invalid, $"{subject}.", string.Join('\n', lines));
    }

    // obj/project.assets.json beside the project file, else in the intermediate output folder or one of its parents, which covers
    // the artifacts layout and a custom BaseIntermediateOutputPath.
    private static bool HasAssetsFile(Project project)
    {
        if (File.Exists(Path.Combine(Path.GetDirectoryName(project.FilePath!)!, "obj", AssetsFileName)))
        {
            return true;
        }

        if (project.CompilationOutputInfo.AssemblyPath is not { Length: > 0 } assemblyPath)
        {
            return false;
        }

        for (var directory = Path.GetDirectoryName(assemblyPath); !string.IsNullOrEmpty(directory); directory = Path.GetDirectoryName(directory))
        {
            if (File.Exists(Path.Combine(directory, AssetsFileName)))
            {
                return true;
            }
        }

        return false;
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

    /// <summary><paramref name="path"/> relative to <paramref name="rootDirectory"/> when it is rooted, else unchanged.</summary>
    public static string Relative(string rootDirectory, string path) =>
        Path.IsPathRooted(path) ? Path.GetRelativePath(rootDirectory, path) : path;

    private static Result<AnalyzedProject, AriadneError> Failure(AriadneError error) =>
        Result<AnalyzedProject, AriadneError>.Failure(error);
}
