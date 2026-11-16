using Ariadne.NET.Abstractions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Ariadne.NET.Workspaces.Tests;

/// <summary>Compiles hand-built projects, to pin the output layouts and file paths a real load rarely produces.</summary>
public sealed class ProjectCompilerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ariadne-compiler-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task AssemblyOutsideAnObjFolder_SkipsMissingFilesBesideTheAssemblyOnly()
    {
        var project = Project("Lib", "namespace Lib; public class A { }", Path.Combine(_dir, "Lib", "Lib.csproj"), Path.Combine(_dir, "out", "bin", "Lib.dll"));
        project = MissingDocument(project, Path.Combine(_dir, "out", "bin", "Gen.cs"));
        project = MissingDocument(project, Path.Combine(_dir, "Lib", "Gone.cs"));

        var result = await ProjectCompiler.CompileAsync(project, _dir, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Project 'Lib' lists a source file that does not exist.");
        result.Error.Detail.Should().Be($"{Path.Combine("Lib", "Gone.cs")}: file not found");
    }

    [Fact]
    public async Task ProjectWithEmptyFileAndAssemblyPaths_ReportsAMissingSourceFile()
    {
        var project = Project("Bare", "namespace Bare; public class A { }", filePath: "", assemblyPath: "");
        project = MissingDocument(project, Path.Combine(_dir, "obj", "Gone.cs"));

        var result = await ProjectCompiler.CompileAsync(project, _dir, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Detail.Should().Be($"{Path.Combine("obj", "Gone.cs")}: file not found", "an empty project path names no obj folder to skip");
    }

    [Fact]
    public async Task CompileErrorWithAnAssetsFileBesideTheProject_HasNoRestoreHint()
    {
        var file = Path.Combine(_dir, "Has", "Has.csproj");
        Directory.CreateDirectory(Path.Combine(_dir, "Has", "obj"));
        await File.WriteAllTextAsync(Path.Combine(_dir, "Has", "obj", ProjectCompiler.AssetsFileName), "{}");
        var project = Project("Has", "namespace Has; public class A : Missing { }", file, assemblyPath: null);

        var result = await ProjectCompiler.CompileAsync(project, _dir, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Project 'Has' has 1 compile error.");
        result.Error.Detail.Should().NotContain(ProjectCompiler.RestoreHint);
    }

    [Fact]
    public async Task CompileErrorWithAnEmptyProjectPath_HasNoRestoreHint()
    {
        var project = Project("Empty", "namespace Empty; public class A : Missing { }", filePath: "", assemblyPath: null);

        var result = await ProjectCompiler.CompileAsync(project, _dir, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Project 'Empty' has 1 compile error.");
        result.Error.Detail.Should().NotContain(ProjectCompiler.RestoreHint);
    }

    [Fact]
    public async Task CompileErrorThatNamesTheAssetsFile_HasTheRestoreHintEvenWithAnAssetsFile()
    {
        var file = Path.Combine(_dir, "Named", "Named.csproj");
        Directory.CreateDirectory(Path.Combine(_dir, "Named", "obj"));
        await File.WriteAllTextAsync(Path.Combine(_dir, "Named", "obj", ProjectCompiler.AssetsFileName), "{}");
        var project = Project("Named", "#error project.assets.json is stale\nnamespace Named;", file, assemblyPath: null);

        var result = await ProjectCompiler.CompileAsync(project, _dir, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be($"Project 'Named' has 1 compile error{ProjectCompiler.RestoreSuffix}.");
        result.Error.Detail!.Split('\n').Should().Equal(
            "A.cs:1:8: CS1029 #error: 'project.assets.json is stale'",
            ProjectCompiler.RestoreHint);
    }

    private static Project Project(string name, string source, string? filePath, string? assemblyPath)
    {
        var info = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            name,
            name,
            LanguageNames.CSharp,
            filePath: filePath,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        info = info.WithCompilationOutputInfo(info.CompilationOutputInfo.WithAssemblyPath(assemblyPath));
        var workspace = new AdhocWorkspace();
        return workspace.AddProject(info).AddDocument("A.cs", SourceText.From(source)).Project;
    }

    private static Project MissingDocument(Project project, string filePath) =>
        project.AddDocument(Path.GetFileName(filePath), SourceText.From(""), filePath: filePath).Project;
}
