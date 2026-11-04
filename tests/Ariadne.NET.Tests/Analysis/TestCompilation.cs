using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Ariadne.NET.Tests.Analysis;

/// <summary>Builds in-memory C# compilations for analysis tests.</summary>
internal static class TestCompilation
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> PlatformReferences = new(LoadPlatformReferences);

    public static CSharpCompilation Create(string source) => Create(("Test.cs", source));

    public static CSharpCompilation Create(params (string Path, string Source)[] files) =>
        Create(extraReferences: [], files);

    public static CSharpCompilation Create(IReadOnlyList<MetadataReference> extraReferences, params (string Path, string Source)[] files)
    {
        ArgumentNullException.ThrowIfNull(extraReferences);
        ArgumentNullException.ThrowIfNull(files);

        var trees = files
            .Select(f => CSharpSyntaxTree.ParseText(f.Source, path: f.Path))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            trees,
            [.. PlatformReferences.Value, .. extraReferences],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var errors = new List<string>();
        foreach (var diagnostic in compilation.GetDiagnostics())
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                errors.Add(diagnostic.ToString());
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Test compilation has errors:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}"));
        }

        return compilation;
    }

    private static IReadOnlyList<MetadataReference> LoadPlatformReferences()
    {
        var tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is not available.");
        return tpa
            .Split(Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
    }
}
