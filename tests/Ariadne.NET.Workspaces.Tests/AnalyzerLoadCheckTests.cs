using System.Buffers.Binary;
using Ariadne.NET.Abstractions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Ariadne.NET.Workspaces.Tests;

public sealed class AnalyzerLoadCheckTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ariadne-analyzer-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void RunningCompilerVersion_IsTheLoadedRoslyn() =>
        AnalyzerLoadCheck.RunningCompilerVersion.Should().Be(typeof(Compilation).Assembly.GetName().Version);

    [Fact]
    public void AnalyzerOnTheRunningCompiler_HasNoProblem()
    {
        var path = EmitAnalyzer("Current", AnalyzerLoadCheck.RunningCompilerVersion);

        AnalyzerLoadCheck.Problem(path).Should().BeNull();
    }

    [Fact]
    public void AnalyzerWithoutACompilerReference_HasNoProblem()
    {
        var path = Emit("Plain", "public class Plain { }", []);

        AnalyzerLoadCheck.Problem(path).Should().BeNull();
    }

    [Fact]
    public void AnalyzerOnANewerCompiler_NamesBothVersions()
    {
        var path = EmitAnalyzer("Newer", new Version(99, 1, 0, 0));

        AnalyzerLoadCheck.Problem(path).Should().Be(
            $"built against Microsoft.CodeAnalysis 99.1.0.0, newer than the {AnalyzerLoadCheck.RunningCompilerVersion} that "
            + "Ariadne.NET runs; an Ariadne.NET release on Microsoft.CodeAnalysis 99.1.0.0 or later can load it");
    }

    [Fact]
    public void MissingFile_IsNotFound() =>
        AnalyzerLoadCheck.Problem(Path.Combine(_dir, "Missing.dll")).Should().Be("file not found");

    [Fact]
    public void TextFile_IsNotAnAssembly()
    {
        var path = Path.Combine(_dir, "Text.dll");
        File.WriteAllText(path, "not an assembly");

        AnalyzerLoadCheck.Problem(path).Should().Be("not a .NET assembly");
    }

    [Fact]
    public void PeWithoutMetadata_IsNotAnAssembly()
    {
        var path = Emit("Native", "public class Native { }", []);
        var bytes = File.ReadAllBytes(path);
        ClearClrHeader(bytes);
        File.WriteAllBytes(path, bytes);

        AnalyzerLoadCheck.Problem(path).Should().Be("not a .NET assembly");
    }

    [Fact]
    public void LockedFile_CannotBeRead()
    {
        var path = Emit("Locked", "public class Locked { }", []);
        using var held = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        AnalyzerLoadCheck.Problem(path).Should().StartWith("cannot be read: ");
    }

    [Fact]
    public void Failures_ShowPathsUnderTheRootRelativeAndOthersInFull()
    {
        var root = Directory.CreateDirectory(Path.Combine(_dir, "repo")).FullName;
        var inside = Path.Combine(root, "Inside.dll");
        var outside = Path.Combine(_dir, "Outside.dll");
        File.WriteAllText(inside, "not an assembly");
        var fine = EmitAnalyzer("Fine", AnalyzerLoadCheck.RunningCompilerVersion);

        // An analyzer reference held in memory has no path and is skipped.
        var project = ProjectWithAnalyzers("Two", "public class Two { }", inside, outside, fine)
            .AddAnalyzerReference(new AnalyzerImageReference([]));

        AnalyzerLoadCheck.Failures(project, root).Should().Equal(
            ("Inside", "Inside.dll: not a .NET assembly"),
            ("Outside", $"{outside}: file not found"));
    }

    [Fact]
    public async Task CompileErrorsAfterTwoAnalyzerFailures_SayHowMany()
    {
        var project = ProjectWithAnalyzers(
            "Two",
            "public class Two { public Generated.Missing? Value; }",
            Path.Combine(_dir, "First.dll"),
            Path.Combine(_dir, "Second.dll"));

        var result = await ProjectCompiler.CompileAsync(project, _dir, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be(
            "Project 'Two' has 1 compile error, and 2 analyzers could not be loaded, so their source generators did not run.");
        result.Error.Detail!.Split('\n')[..2].Should().Equal("First.dll: file not found", "Second.dll: file not found");
    }

    // Zeroes the CLI header entry of the PE data directories, which leaves a valid PE that holds no metadata.
    private static void ClearClrHeader(byte[] image)
    {
        var pe = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x3C));
        var optional = pe + 4 + 20;
        var directories = optional + (BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(optional)) == 0x20B ? 112 : 96);
        image.AsSpan(directories + (14 * 8), 8).Clear();
    }

    private static Project ProjectWithAnalyzers(string name, string source, params string[] analyzerPaths)
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            name,
            name,
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable),
            metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            analyzerReferences: [.. analyzerPaths.Select(p => new AnalyzerFileReference(p, new NoLoader()))]));
        return project.AddDocument("Source.cs", SourceText.From(source)).Project;
    }

    // The check reads metadata only, so nothing is ever loaded through this.
    private sealed class NoLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
        }

        public System.Reflection.Assembly LoadFromPath(string fullPath) =>
            throw new InvalidOperationException($"Loading {fullPath} is not expected.");
    }

    // An analyzer assembly that references a stand-in Microsoft.CodeAnalysis with the given version, which is all the check reads.
    private string EmitAnalyzer(string name, Version compilerVersion)
    {
        var stub = CSharpCompilation.Create(
            "Microsoft.CodeAnalysis",
            [CSharpSyntaxTree.ParseText(
                $"[assembly: System.Reflection.AssemblyVersion(\"{compilerVersion}\")]\nnamespace Microsoft.CodeAnalysis {{ public class Stub {{ }} }}")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        stub.Emit(stream).Success.Should().BeTrue();
        return Emit(name, "public class Uses { public Microsoft.CodeAnalysis.Stub? Stub; }", [MetadataReference.CreateFromImage(stream.ToArray())]);
    }

    private string Emit(string name, string source, IReadOnlyList<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location), .. references],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var path = Path.Combine(_dir, name + ".dll");
        using (var file = File.Create(path))
        {
            var result = compilation.Emit(file);
            result.Success.Should().BeTrue(string.Join('\n', result.Diagnostics.Select(d => d.ToString())));
        }

        return path;
    }
}
