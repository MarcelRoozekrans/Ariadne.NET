using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;
using FluentAssertions;
using Xunit.Abstractions;

namespace Ariadne.NET.Hooks.Tests;

public class NewFilePlacementTests(ITestOutputHelper output)
{
    private static readonly RulePack Pack = new()
    {
        Name = "shop",
        Layers =
        [
            new LayerDefinition { Name = "Domain", Id = "S-D", Because = "x", Assemblies = ["Shop.Domain"] },
            new LayerDefinition { Name = "Agents", Id = "S-A", Because = "x", Namespaces = ["Shop.Agents.Runtime.**"], Assemblies = ["Shop.Agents"] },
        ],
    };

    private static void Csproj(TempProject project, string folder, string name, string properties = "")
    {
        var dir = Path.Combine(project.Root, folder);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, $"{name}.csproj"),
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>{properties}</PropertyGroup></Project>");
    }

    [Fact]
    public void A_new_file_in_a_selected_assembly_is_allowed()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Domain", "Shop.Domain");

        NewFilePlacement.Check(Pack, project.Root, Path.Combine(project.Root, "Shop.Domain", "Order.cs"), "class A;")
            .Should().BeNull();
    }

    [Fact]
    public void A_new_file_in_an_unselected_project_gets_a_question()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Web", "Shop.Web");

        var question = NewFilePlacement.Check(Pack, project.Root, Path.Combine(project.Root, "Shop.Web", "Pages", "Home.cs"), "class A;");

        question.Should().Be(
            "New file 'Shop.Web/Pages/Home.cs' would be namespace 'Shop.Web.Pages' in assembly 'Shop.Web', "
            + "which no layer of the rules selects. Layers: Domain, Agents. Ask the user where it belongs or add a layer.");
    }

    [Fact]
    public void Backslashes_and_slashes_give_the_same_answer()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Web", "Shop.Web");
        var slashes = Path.Combine(project.Root, "Shop.Web", "Pages", "Home.cs");
        var windows = project.Root + "\\Shop.Web\\Pages\\Home.cs";

        var expected = NewFilePlacement.Check(Pack, project.Root, slashes, null);

        expected.Should().NotBeNull();
        NewFilePlacement.Check(Pack, project.Root + "\\", windows.Replace('/', '\\'), null).Should().Be(expected);
    }

    [Fact]
    public void A_folder_with_spaces_works()
    {
        using var project = new TempProject();
        Csproj(project, "Shop Web", "Shop.Web");

        var question = NewFilePlacement.Check(Pack, project.Root, Path.Combine(project.Root, "Shop Web", "My Pages", "Home.cs"), null);

        question.Should().Contain("namespace 'Shop.Web.My Pages'").And.Contain("assembly 'Shop.Web'");
    }

    [Fact]
    public void AssemblyName_and_RootNamespace_overrides_are_honoured()
    {
        using var project = new TempProject();
        Csproj(project, "Web", "Web", "<AssemblyName>Shop.Domain</AssemblyName>");
        Csproj(project, "Site", "Site", "<AssemblyName>Shop.Site</AssemblyName><RootNamespace>Shop.Root</RootNamespace>");

        NewFilePlacement.Check(Pack, project.Root, Path.Combine(project.Root, "Web", "A.cs"), null).Should().BeNull();
        NewFilePlacement.Check(Pack, project.Root, Path.Combine(project.Root, "Site", "Sub", "A.cs"), null)
            .Should().Contain("namespace 'Shop.Root.Sub'").And.Contain("assembly 'Shop.Site'");
    }

    [Fact]
    public void The_content_namespace_wins_over_the_folder()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Agents", "Shop.Agents");
        var file = Path.Combine(project.Root, "Shop.Agents", "Contracts", "A.cs");

        NewFilePlacement.Check(Pack, project.Root, file, "using System;\nnamespace Shop.Agents.Runtime.Retrieval;\nclass A;")
            .Should().BeNull();
        NewFilePlacement.Check(Pack, project.Root, file, "namespace Shop.Agents.Runtime.Retrieval\n{\n}")
            .Should().BeNull();
        NewFilePlacement.Check(Pack, project.Root, file, "class A;")
            .Should().Contain("namespace 'Shop.Agents.Contracts'");
    }

    [Theory]
    [InlineData("Shop.Web/obj/Debug/A.cs")]
    [InlineData("Shop.Web/bin/A.cs")]
    [InlineData("Shop.Web/Notes.txt")]
    [InlineData("Elsewhere/A.cs")]
    public void Other_files_are_allowed(string relative)
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Web", "Shop.Web");

        NewFilePlacement.Check(Pack, project.Root, Path.Combine(project.Root, relative), null).Should().BeNull();
    }

    [Fact]
    public void An_existing_file_is_allowed_unless_treated_as_new()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Web", "Shop.Web");
        var file = Path.Combine(project.Root, "Shop.Web", "Home.cs");
        File.WriteAllText(file, "class A;");

        NewFilePlacement.Check(Pack, project.Root, file, null).Should().BeNull();
        NewFilePlacement.Check(Pack, project.Root, file, null, treatAsNew: true).Should().NotBeNull();
    }

    [Fact]
    public void A_pack_without_layer_selectors_allows_everything()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Web", "Shop.Web");
        var pack = new RulePack { Name = "x", Layers = [new LayerDefinition { Name = "L", Id = "L", Because = "x" }] };

        NewFilePlacement.Check(pack, project.Root, Path.Combine(project.Root, "Shop.Web", "A.cs"), null).Should().BeNull();
    }

    [Fact]
    public void No_noise_on_the_clean_fixture()
    {
        var pack = Resolve(Path.Combine(RepoRoot, "tests", "fixtures", "clean"), "schema: 2\nname: shop\nextends:\n  - builtin://clean-architecture\n");

        Unplaced(pack, Path.Combine(RepoRoot, "tests", "fixtures", "clean")).Should().BeEmpty();
    }

    [Fact]
    public void No_noise_on_this_repo()
    {
        var pack = Resolve(RepoRoot, File.ReadAllText(Path.Combine(RepoRoot, ".architecture", "rules.yaml")));

        Unplaced(pack, RepoRoot, "src").Should().BeEmpty();
    }

    [Fact]
    public void Only_files_no_layer_of_the_daedalus_fixture_selects_are_questioned()
    {
        var root = Path.Combine(RepoRoot, "tests", "fixtures", "daedalus", "clean");
        var pack = Resolve(root, File.ReadAllText(Path.Combine(RepoRoot, "tests", "fixtures", "daedalus", "daedalus.rules.yaml")));

        var unplaced = Unplaced(pack, root);

        // Rag.NET and Stub.EntityFrameworkCore are in assemblies no layer selects. Daedalus.Agents/AgentDescriptor.cs is in
        // Daedalus.Agents.Contracts, which the Agents layer leaves out on purpose by selecting only Daedalus.Agents.Runtime.**.
        // Every file under the Runtime namespaces, such as RagAgent.cs, is placed because its namespace is read from the content.
        unplaced.Should().BeEquivalentTo(
            "Daedalus.Agents/AgentDescriptor.cs",
            "Rag.NET/Retriever.cs",
            "Stub.EntityFrameworkCore/DbContext.cs");
    }

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".architecture")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
        }
    }

    private static RulePack Resolve(string root, string yaml)
    {
        var loader = new YamlRulePackLoader();
        var registry = new BuiltinPackRegistry(loader);
        var resolver = new RulePackResolver(registry, loader, new RulePackMerger());
        var user = loader.LoadFromString(yaml);
        user.IsSuccess.Should().BeTrue();
        var resolved = resolver.Resolve(user.Value, root);
        resolved.IsSuccess.Should().BeTrue();
        return resolved.Value;
    }

    private List<string> Unplaced(RulePack pack, string root, string? sub = null)
    {
        var result = new List<string>();
        var start = sub is null ? root : Path.Combine(root, sub);
        foreach (var file in Directory.EnumerateFiles(start, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var question = NewFilePlacement.Check(pack, root, file, File.ReadAllText(file), treatAsNew: true);
            if (question is not null)
            {
                output.WriteLine(question);
                result.Add(relative);
            }
        }

        return result;
    }
}
