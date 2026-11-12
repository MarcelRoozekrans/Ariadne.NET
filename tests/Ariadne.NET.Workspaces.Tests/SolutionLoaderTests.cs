using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ariadne.NET.Workspaces.Tests;

[Collection(FixtureSolutionCollection.Name)]
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
        result.Error.Message.Should().Contain("'Unrestored'").And.EndWith("; it may not be restored: run 'dotnet restore' first.");
        result.Error.Detail.Should().Contain("CS0246");
        result.Error.Detail!.Split('\n').Should().Contain(RestoreHint);
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

        var report = new ArchitectureValidator().Validate(TestPacks.Layered("Domain", "Infrastructure"), loaded.Projects);

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
        result.Error.Message.Should().Be("The solution holds no C# project to check.");
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
        AddProject(workspace, "Lib", LanguageNames.CSharp, "namespace One; public class A { }", Path.Combine(_dir, "One", "Lib.csproj"));
        AddProject(workspace, "Lib", LanguageNames.CSharp, "namespace Two; public class B { }", Path.Combine(_dir, "Two", "Lib.csproj"));

        var result = await SolutionLoader.FromSolutionAsync(workspace.CurrentSolution, _dir, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("More than one project is named 'Lib'.");
        result.Error.Detail.Should().Contain(Path.Combine("One", "Lib.csproj")).And.Contain(Path.Combine("Two", "Lib.csproj"));
    }

    [Theory]
    [InlineData("", "docker-compose.dcproj")]
    [InlineData("", "Shared.shproj")]
    [InlineData("My Repo", "docker-compose.dcproj")]
    public async Task LanguagelessProjectFile_IsSkipped(string folder, string projectFile)
    {
        var directory = Directory.CreateDirectory(Path.Combine(_dir, folder)).FullName;
        await File.WriteAllTextAsync(Path.Combine(directory, projectFile), "<Project />");
        var solution = await WriteSolutionAsync(directory, FixtureProjects.Path("clean", "Shop.Domain", "Shop.Domain.csproj"), projectFile);

        var result = await _loader.LoadAsync(solution);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        using var loaded = result.Value;
        loaded.Projects.Select(p => p.Name).Should().Equal("Shop.Domain");
        loaded.SkippedProjects.Should().Equal(new SkippedProject(Path.GetFileNameWithoutExtension(projectFile), "not a C# project"));
    }

    [Fact]
    public async Task LanguagelessProjectNextToBrokenCSharp_KeepsTheCompileError()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "docker-compose.dcproj"), "<Project />");
        var solution = await WriteSolutionAsync(_dir, FixtureProjects.Path("compile-error", "Broken.csproj"), "docker-compose.dcproj");

        var result = await _loader.LoadAsync(solution);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Project 'Broken' has 1 compile error.");
    }

    [Fact]
    public void FailureAboutAnotherProject_IsKeptEvenWhenItMentionsASkippedOne()
    {
        var shared = Path.Combine(_dir, "My Repo", "Shared.shproj");
        var app = Path.Combine(_dir, "My Repo", "App.csproj");
        string[] failures =
        [
            $"Cannot open project '{shared}' because the file extension '.shproj' is not associated with a language.",
            $"Msbuild failed when processing the file '{app}' with message: imported project '{shared}' was not found.",
        ];

        var remaining = SolutionLoader.WithoutFailuresOf(failures, [shared]);

        remaining.Should().Equal(failures[1]);
    }

    [Fact]
    public async Task ArtifactsOutputLayout_LoadsWhenRestored()
    {
        var project = await CreateArtifactsProjectAsync("namespace Art; public class Order { }");

        var result = await _loader.LoadAsync(project);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        using var loaded = result.Value;
        loaded.Projects.Select(p => p.Name).Should().Equal("Art");
    }

    [Fact]
    public async Task ArtifactsOutputLayout_MissingCompileItemUnderArtifactsObj_LoadsClean()
    {
        // The obj folder is found above the intermediate assembly in artifacts/obj/Art/, not beside the project file.
        var missing = Path.Combine(_dir, "artifacts", "obj", "Art", "Gone.g.cs");
        var project = await CreateArtifactsProjectAsync(
            "namespace Art; public class Order { }",
            "<ItemGroup><Compile Include=\"../artifacts/obj/Art/Gone.g.cs\" /></ItemGroup>");
        File.Exists(missing).Should().BeFalse();

        var result = await _loader.LoadAsync(project);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        using var loaded = result.Value;
        loaded.Projects.Select(p => p.Name).Should().Equal("Art");
    }

    [Fact]
    public async Task ArtifactsOutputLayoutWithCompileError_HasNoRestoreHint()
    {
        // The assets file is found above the intermediate output, so the error is not blamed on a missing restore.
        var project = await CreateArtifactsProjectAsync("namespace Art; public class Order { int Id = \"x\"; }");

        var result = await _loader.LoadAsync(project);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Project 'Art' has 1 compile error.");
        result.Error.Detail.Should().NotContain(RestoreHint);
    }

    [Fact]
    public async Task UnrestoredProjectThatCompiles_Loads()
    {
        // A project with no packages that targets the SDK's own framework compiles completely without a restore.
        var project = await CreatePlainProjectAsync("Plain", "");

        var result = await _loader.LoadAsync(project);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        using var loaded = result.Value;
        loaded.Projects.Select(p => p.Name).Should().Equal("Plain");
        File.Exists(Path.Combine(_dir, "Plain", "obj", "project.assets.json")).Should().BeFalse();
    }

    [Fact]
    public async Task ExplicitCompileItemForMissingFile_IsInvalid()
    {
        var project = await CreatePlainProjectAsync("Gone", """<ItemGroup><Compile Include="Gone.cs" /></ItemGroup>""");

        var result = await _loader.LoadAsync(project);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Project 'Gone' lists a source file that does not exist.");
        result.Error.Detail.Should().Be("Gone.cs: file not found", "paths are relative to the loaded project's directory");
    }

    [Fact]
    public async Task MissingCompileItemUnderObj_Loads()
    {
        // A target that writes a file under obj/ only during a real build leaves a Compile item the design-time build cannot see.
        var project = await CreatePlainProjectAsync("Later", """<ItemGroup><Compile Include="obj/Generated/Later.g.cs" /></ItemGroup>""");

        var result = await _loader.LoadAsync(project);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        result.Value.Dispose();
    }

    [Fact]
    public async Task ProjectWithoutCoreLibrary_IsInvalid()
    {
        using var workspace = new AdhocWorkspace();
        var info = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            "Bare",
            "Bare",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var project = workspace.AddProject(info);
        workspace.AddDocument(project.Id, "A.cs", SourceText.From("namespace Bare; public class A { }"));

        var result = await SolutionLoader.FromSolutionAsync(workspace.CurrentSolution, _dir, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Project 'Bare' cannot resolve System.Object.");
    }

    [Fact]
    public async Task ManyCompileErrors_ListsTheFirstFive()
    {
        using var workspace = new AdhocWorkspace();
        var fields = string.Concat(Enumerable.Range(1, 7).Select(i => $"int F{i} = \"x\";\n"));
        AddProject(workspace, "Errors", LanguageNames.CSharp, $"namespace Errors;\npublic class A\n{{\n{fields}}}\n");

        var result = await SolutionLoader.FromSolutionAsync(workspace.CurrentSolution, _dir, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Project 'Errors' has 7 compile errors.");
        result.Error.Detail!.Split('\n').Should().HaveCount(5).And.OnlyContain(l => l.Contains("CS0029", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingSourceFile_IsInvalidAndNamesIt()
    {
        using var workspace = new AdhocWorkspace();
        var project = AddProject(workspace, "Lib", LanguageNames.CSharp, "namespace Lib; public class A { }");
        workspace.AddDocument(DocumentInfo.Create(
            DocumentId.CreateNewId(project.Id),
            "Gone.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(""), VersionStamp.Default)),
            filePath: Path.Combine(_dir, "Gone.cs")));

        var result = await SolutionLoader.FromSolutionAsync(workspace.CurrentSolution, _dir, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Be("Project 'Lib' lists a source file that does not exist.");
        result.Error.Detail.Should().Be("Gone.cs: file not found");
    }

    [Fact]
    public async Task ParenthesisedProjectFileName_IsNotATargetFramework()
    {
        using var workspace = new AdhocWorkspace();
        AddProject(workspace, "Lib (Legacy)", LanguageNames.CSharp, "namespace Legacy; public class A { }", Path.Combine(_dir, "Lib (Legacy).csproj"));
        AddProject(workspace, "Multi(net9.0)", LanguageNames.CSharp, "namespace Multi; public class B { }", Path.Combine(_dir, "Multi.csproj"));

        var result = await SolutionLoader.FromSolutionAsync(workspace.CurrentSolution, _dir, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        using var loaded = result.Value;
        loaded.Projects.Select(p => (p.Name, p.TargetFramework)).Should().BeEquivalentTo(
            [("Lib (Legacy)", (string?)null), ("Multi", "net9.0")]);
    }

    private static bool IsUnderObj(string path) =>
        path.Contains("/obj/", StringComparison.Ordinal) || path.Contains("\\obj\\", StringComparison.Ordinal);

    private static async Task<string> WriteSolutionAsync(string directory, params string[] projects)
    {
        var solution = Path.Combine(directory, "Mixed.slnx");
        var entries = string.Concat(projects.Select(p => $"""<Project Path="{p}" />"""));
        await File.WriteAllTextAsync(solution, $"<Solution>{entries}</Solution>").ConfigureAwait(false);
        return solution;
    }

    // An unrestored net10.0 project with one hand-written type; it compiles without a restore.
    private async Task<string> CreatePlainProjectAsync(string name, string items)
    {
        var project = Path.Combine(_dir, name, name + ".csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        await File.WriteAllTextAsync(
            project,
            $"""<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>{items}</Project>""")
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(_dir, name, "Order.cs"), $"namespace {name}; public class Order {{ }}").ConfigureAwait(false);
        return project;
    }

    // A restored project under UseArtifactsOutput, whose assets file sits in artifacts/obj/Art/, not in obj/ beside the project.
    private async Task<string> CreateArtifactsProjectAsync(string source, string items = "")
    {
        var project = Path.Combine(_dir, "Art", "Art.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        await File.WriteAllTextAsync(
            Path.Combine(_dir, "Directory.Build.props"),
            "<Project><PropertyGroup><UseArtifactsOutput>true</UseArtifactsOutput></PropertyGroup></Project>").ConfigureAwait(false);
        await File.WriteAllTextAsync(
            project,
            $"""<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>{items}</Project>""")
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(_dir, "Art", "Order.cs"), source).ConfigureAwait(false);
        Restore(project);
        File.Exists(Path.Combine(_dir, "Art", "obj", "project.assets.json")).Should().BeFalse("the artifacts layout moves obj/");
        return project;
    }

    private static void Restore(string project)
    {
        var info = FixtureProjects.DotnetStartInfo("restore", project);
        using var process = System.Diagnostics.Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit(TimeSpan.FromMinutes(5)).Should().BeTrue("dotnet restore should finish");
        process.ExitCode.Should().Be(0, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
    }

    private static Project AddProject(AdhocWorkspace workspace, string name, string language, string? source = null, string? filePath = null)
    {
        var info = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            name,
            name,
            language,
            filePath: filePath,
            compilationOptions: string.Equals(language, LanguageNames.CSharp, StringComparison.Ordinal)
                ? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                : null,
            metadataReferences: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var project = workspace.AddProject(info);
        if (source is not null)
        {
            project = workspace.AddDocument(project.Id, "A.cs", SourceText.From(source)).Project;
        }

        return project;
    }

    private async Task<LoadedSolution> LoadAsync(params string[] parts)
    {
        var result = await _loader.LoadAsync(FixtureProjects.Path(parts)).ConfigureAwait(false);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : null);
        return result.Value;
    }
}
