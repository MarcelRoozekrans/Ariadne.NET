using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
