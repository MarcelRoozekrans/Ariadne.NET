using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ariadne.NET.Workspaces.Tests;

public sealed class SolutionLoaderTests : IDisposable
{
    private const string RestoreHint = "run 'dotnet restore' first";

    private readonly SolutionLoader _loader = new();
    private readonly string _dir = Directory.CreateTempSubdirectory("ariadne-loader-").FullName;

    public SolutionLoaderTests() => FixtureProjects.EnsureRestored();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task CleanSlnx_LoadsBothProjects()
    {
        using var loaded = await LoadAsync("clean", "Shop.slnx");

        loaded.RootDirectory.Should().Be(FixtureProjects.Path("clean"));
        loaded.Projects.Select(p => p.Name).Should().BeEquivalentTo("Shop.Domain", "Shop.WebApi");
        loaded.Projects.Should().OnlyContain(p => p.TargetFramework == null);
        loaded.SkippedProjects.Should().BeEmpty();
    }

    [Fact]
    public async Task CleanSln_LoadsBothProjects()
    {
        using var loaded = await LoadAsync("clean", "Shop.sln");

        loaded.Projects.Select(p => p.Name).Should().BeEquivalentTo("Shop.Domain", "Shop.WebApi");
    }

    [Fact]
    public async Task Csproj_LoadsOneProject()
    {
        // Shop.WebApi references Shop.Domain, which the load pulls in as a reference only.
        using var loaded = await LoadAsync("clean", "Shop.WebApi", "Shop.WebApi.csproj");

        loaded.RootDirectory.Should().Be(FixtureProjects.Path("clean", "Shop.WebApi"));
        loaded.Projects.Select(p => p.Name).Should().Equal("Shop.WebApi");
        loaded.SkippedProjects.Should().BeEmpty();
    }

    [Fact]
    public async Task Slnf_LoadsOnlyListedProjects()
    {
        using var loaded = await LoadAsync("broken", "Shop.Infrastructure-only.slnf");

        loaded.RootDirectory.Should().Be(FixtureProjects.Path("broken"));
        loaded.Projects.Select(p => p.Name).Should().Equal("Shop.Infrastructure");
    }

    [Fact]
    public async Task SlnfListingAReferencingProject_DoesNotAnalyseTheReferencedOne()
    {
        var filter = Path.Combine(_dir, "Domain-only.slnf");
        await File.WriteAllTextAsync(
            filter,
            $$"""
            { "solution": { "path": {{System.Text.Json.JsonSerializer.Serialize(FixtureProjects.Path("broken", "Shop.slnx"))}}, "projects": [ "Shop.Domain/Shop.Domain.csproj" ] } }
            """);

        var result = await _loader.LoadAsync(filter);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        using var loaded = result.Value;
        loaded.Projects.Select(p => p.Name).Should().Equal("Shop.Domain");
        loaded.SkippedProjects.Should().BeEmpty();
    }

    [Fact]
    public async Task SlnxListingAReferencingProject_DoesNotAnalyseTheReferencedOne()
    {
        // MSBuildWorkspace returns Shop.Infrastructure too, because Shop.Domain references it.
        var solution = Path.Combine(_dir, "Domain-only.slnx");
        await File.WriteAllTextAsync(
            solution,
            $"""<Solution><Project Path="{FixtureProjects.Path("broken", "Shop.Domain", "Shop.Domain.csproj")}" /></Solution>""");

        var result = await _loader.LoadAsync(solution);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        using var loaded = result.Value;
        loaded.Projects.Select(p => p.Name).Should().Equal("Shop.Domain");
        loaded.SkippedProjects.Should().BeEmpty();
    }

    [Fact]
    public async Task MalformedSlnf_IsInvalid()
    {
        var filter = Path.Combine(_dir, "Bad.slnf");
        await File.WriteAllTextAsync(filter, """{ "solution": { "projects": [] } }""");

        var result = await _loader.LoadAsync(filter);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("Bad.slnf").And.EndWith(".");
    }

    [Fact]
    public async Task MultiTarget_LoadsOneAnalyzedProjectPerFramework()
    {
        using var loaded = await LoadAsync("multitarget", "Multi.csproj");

        loaded.Projects.Select(p => p.Name).Should().Equal("Multi", "Multi");
        loaded.Projects.Select(p => p.TargetFramework).Should().BeEquivalentTo("net10.0", "net9.0");
    }

    [Fact]
    public async Task CompileError_IsInvalidAndNamesProject()
    {
        var result = await _loader.LoadAsync(FixtureProjects.Path("compile-error", "Broken.csproj"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("'Broken'").And.EndWith(".");
        result.Error.Message.Should().NotContain(RestoreHint);
        result.Error.Detail.Should().Contain("Broken.cs:5:").And.Contain("CS0029");
    }

    [Fact]
    public async Task Unrestored_IsInvalidAndSaysRestore()
    {
        var project = FixtureProjects.Path("unrestored", "Unrestored.csproj");

        var result = await _loader.LoadAsync(project);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("'Unrestored'").And.Contain(RestoreHint).And.EndWith(".");
        File.Exists(FixtureProjects.Path("unrestored", "obj", "project.assets.json")).Should().BeFalse("the loader never restores");
    }

    [Fact]
    public async Task GeneratorOutputWithPlainName_IsInGeneratedTrees()
    {
        using var loaded = await LoadAsync("generator", "Gen.App", "Gen.App.csproj");

        var app = loaded.Projects.Should().ContainSingle().Which;
        app.Name.Should().Be("Gen.App");
        app.GeneratedTrees.Select(t => Path.GetFileName(t.FilePath)).Should().Equal("Registrations.cs");
        app.GeneratedTrees.Should().OnlyContain(t => app.Compilation.SyntaxTrees.Contains(t));
    }

    [Fact]
    public async Task ObjGlobalUsings_IsNotASource()
    {
        using var loaded = await LoadAsync("broken", "Shop.slnx");
        foreach (var project in loaded.Projects)
        {
            project.Compilation.SyntaxTrees.Should().Contain(t => IsUnderObj(t.FilePath), "the SDK writes generated files under obj/");
        }

        var report = new ArchitectureValidator().Validate(TestPacks.Layered(), loaded.Projects);

        report.Violations.Should().NotBeEmpty();
        report.Violations.Should().OnlyContain(v => v.Location == null || !IsUnderObj(v.Location.Value.FilePath));
        var handWrittenTypes = loaded.Projects
            .SelectMany(p => p.Compilation.SyntaxTrees)
            .Where(t => !IsUnderObj(t.FilePath))
            .Sum(t => t.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Count());
        report.TypesChecked.Should().Be(handWrittenTypes, "no type declared under obj/ is a source type");
    }

    [Fact]
    public async Task NonCSharpProject_IsSkipped()
    {
        using var workspace = new AdhocWorkspace();
        AddProject(workspace, "VbLib", LanguageNames.VisualBasic);
        AddProject(workspace, "CsLib", LanguageNames.CSharp, "namespace CsLib; public class A { }");

        var result = await SolutionLoader.FromSolutionAsync(workspace.CurrentSolution, _dir, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        using var loaded = result.Value;
        loaded.RootDirectory.Should().Be(_dir);
        loaded.Projects.Select(p => p.Name).Should().Equal("CsLib");
        loaded.SkippedProjects.Should().Equal(new SkippedProject("VbLib", "not a C# project"));
    }

    [Fact]
    public async Task NoCSharpProjects_IsInvalid()
    {
        using var workspace = new AdhocWorkspace();
        AddProject(workspace, "VbLib", LanguageNames.VisualBasic);

        var result = await SolutionLoader.FromSolutionAsync(workspace.CurrentSolution, _dir, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().EndWith(".");
        result.Error.Detail.Should().Contain("VbLib: not a C# project");
    }

    [Fact]
    public async Task EmptySolution_IsInvalid()
    {
        var solution = Path.Combine(_dir, "Empty.slnx");
        await File.WriteAllTextAsync(solution, "<Solution />");

        var result = await _loader.LoadAsync(solution);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
    }

    [Fact]
    public async Task AllProjectsFailToLoad_IsInvalid()
    {
        var solution = Path.Combine(_dir, "Missing.slnx");
        await File.WriteAllTextAsync(solution, """<Solution><Project Path="Gone/Gone.csproj" /></Solution>""");

        var result = await _loader.LoadAsync(solution);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Project 'Gone' did not load.");
        result.Error.Detail.Should().Contain("Gone.csproj");
    }

    [Theory]
    [InlineData("Garbled.slnx", "<Solution><Project")]
    [InlineData("Garbled.sln", "this is not a solution")]
    public async Task MalformedSolution_IsInvalid(string name, string content)
    {
        var solution = Path.Combine(_dir, name);
        await File.WriteAllTextAsync(solution, content);

        var result = await _loader.LoadAsync(solution);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain(name).And.EndWith(".");
    }

    [Fact]
    public async Task MalformedProject_IsInvalidWithoutRestoreHint()
    {
        var project = Path.Combine(_dir, "Garbled.csproj");
        await File.WriteAllTextAsync(project, """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>""");

        var result = await _loader.LoadAsync(project);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("'Garbled'").And.NotContain(RestoreHint);
    }

    [Fact]
    public async Task DuplicateProjectName_IsInvalid()
    {
        using var workspace = new AdhocWorkspace();
        AddProject(workspace, "Lib", LanguageNames.CSharp, "namespace One; public class A { }");
        AddProject(workspace, "Lib", LanguageNames.CSharp, "namespace Two; public class B { }");

        var result = await SolutionLoader.FromSolutionAsync(workspace.CurrentSolution, _dir, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain("'Lib'").And.EndWith(".");
    }

    private static bool IsUnderObj(string path) =>
        path.Contains("/obj/", StringComparison.Ordinal) || path.Contains("\\obj\\", StringComparison.Ordinal);

    private static void AddProject(AdhocWorkspace workspace, string name, string language, string? source = null)
    {
        var info = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            name,
            name,
            language,
            compilationOptions: string.Equals(language, LanguageNames.CSharp, StringComparison.Ordinal)
                ? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                : null,
            metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var project = workspace.AddProject(info);
        if (source is not null)
        {
            workspace.AddDocument(project.Id, "A.cs", SourceText.From(source));
        }
    }

    private async Task<LoadedSolution> LoadAsync(params string[] parts)
    {
        var result = await _loader.LoadAsync(FixtureProjects.Path(parts)).ConfigureAwait(false);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        return result.Value;
    }
}
