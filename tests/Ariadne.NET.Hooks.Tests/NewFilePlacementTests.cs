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

    private const string Instruction = "Ask the user where it belongs or add a layer.";

    [Fact]
    public void A_huge_namespace_gives_a_capped_question_that_still_ends_with_the_instruction()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Web", "Shop.Web");

        var question = NewFilePlacement.Check(
            Pack, project.Root, Path.Combine(project.Root, "Shop.Web", "A.cs"), "namespace " + new string('n', 50_000) + ";");

        question.Should().NotBeNull();
        question!.Length.Should().BeLessThan(3_000);
        question.Should().EndWith(Instruction).And.Contain("…");
    }

    [Fact]
    public void A_very_long_relative_path_gives_a_capped_question_that_still_ends_with_the_instruction()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Web", "Shop.Web");
        var deep = Path.Combine([project.Root, "Shop.Web", .. Enumerable.Repeat(new string('d', 100), 200), "A.cs"]);

        var question = NewFilePlacement.Check(Pack, project.Root, deep, null);

        question.Should().NotBeNull();
        question!.Length.Should().BeLessThan(3_000);
        question.Should().EndWith(Instruction).And.Contain("…");
    }

    [Fact]
    public void Five_hundred_layers_give_a_capped_question_that_still_ends_with_the_instruction()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Web", "Shop.Web");
        var layers = new List<LayerDefinition>();
        for (var i = 0; i < 500; i++)
        {
            layers.Add(new LayerDefinition { Name = $"Layer{i}-{new string('l', 80)}", Id = $"S-{i}", Because = "x", Assemblies = [$"Shop.Other{i}"] });
        }

        var question = NewFilePlacement.Check(new RulePack { Name = "shop", Layers = layers }, project.Root, Path.Combine(project.Root, "Shop.Web", "A.cs"), null);

        question.Should().NotBeNull();
        question!.Length.Should().BeLessThan(3_000);
        question.Should().EndWith(Instruction).And.Contain("more");
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

        question.Should().Contain("namespace 'Shop.Web.My_Pages'").And.Contain("assembly 'Shop.Web'");
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
        var root = Path.Combine(RepoRoot, "tests", "fixtures", "clean");
        var pack = Resolve(root, "schema: 2\nname: shop\nextends:\n  - builtin://clean-architecture\n");

        var (unplaced, evaluated) = Evaluate(pack, root);

        unplaced.Should().BeEmpty();
        evaluated.Should().Contain("Shop.Domain/Order.cs").And.HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void No_noise_on_this_repo()
    {
        var pack = Resolve(RepoRoot, File.ReadAllText(Path.Combine(RepoRoot, ".architecture", "rules.yaml")));

        var (unplaced, evaluated) = Evaluate(pack, RepoRoot, "src");

        unplaced.Should().BeEmpty();
        evaluated.Should().Contain("src/Ariadne.NET/Analysis/LayerProbe.cs").And.Contain("src/Ariadne.NET.Hooks/PreToolUseHook.cs");
        evaluated.Count.Should().BeGreaterThan(100);
    }

    [Fact]
    public void Only_files_no_layer_of_the_daedalus_fixture_selects_are_questioned()
    {
        var root = Path.Combine(RepoRoot, "tests", "fixtures", "daedalus", "clean");
        var pack = Resolve(root, File.ReadAllText(Path.Combine(RepoRoot, "tests", "fixtures", "daedalus", "daedalus.rules.yaml")));

        var (unplaced, evaluated) = Evaluate(pack, root);

        // Rag.NET and Stub.EntityFrameworkCore are in assemblies no layer selects. Daedalus.Agents/AgentDescriptor.cs is in
        // Daedalus.Agents.Contracts, which the Agents layer leaves out on purpose by selecting only Daedalus.Agents.Runtime.**.
        // Every file under the Runtime namespaces, such as RagAgent.cs, is placed because its namespace is read from the content.
        unplaced.Should().BeEquivalentTo(
            "Daedalus.Agents/AgentDescriptor.cs",
            "Rag.NET/Retriever.cs",
            "Stub.EntityFrameworkCore/DbContext.cs");
        evaluated.Should().Contain("Daedalus.Agents/RagAgent.cs").And.HaveCountGreaterThanOrEqualTo(14);
    }

    [Fact]
    public void A_path_that_leaves_the_root_is_allowed()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Web", "Shop.Web");

        NewFilePlacement.Check(Pack, project.Root, Path.Combine(project.Root, "Shop.Web", "..", "..", "Shop.Web", "A.cs"), null).Should().BeNull();
        NewFilePlacement.Check(Pack, project.Root, "..\\Shop.Web\\A.cs", null).Should().BeNull();
    }

    [Fact]
    public void Folder_names_become_identifiers()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Web", "Shop.Web");

        NewFilePlacement.Check(Pack, project.Root, Path.Combine(project.Root, "Shop.Web", "2024", "my-pages", "A.cs"), null)
            .Should().Contain("namespace 'Shop.Web._2024.my_pages'");
    }

    [Fact]
    public void A_malformed_project_file_falls_back_to_its_file_name()
    {
        using var project = new TempProject();
        Directory.CreateDirectory(Path.Combine(project.Root, "Shop.Web"));
        File.WriteAllText(Path.Combine(project.Root, "Shop.Web", "Shop.Web.csproj"), "<Project><PropertyGroup><AssemblyName>Shop.Domain");

        NewFilePlacement.Check(Pack, project.Root, Path.Combine(project.Root, "Shop.Web", "A.cs"), null)
            .Should().Contain("assembly 'Shop.Web'");
    }

    [Fact]
    public void A_non_literal_AssemblyName_falls_back_to_the_file_name()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Web", "Shop.Web", "<AssemblyName>$(Prefix).Core</AssemblyName><RootNamespace>$(Prefix)</RootNamespace>");

        NewFilePlacement.Check(Pack, project.Root, Path.Combine(project.Root, "Shop.Web", "A.cs"), null)
            .Should().Contain("assembly 'Shop.Web'").And.Contain("namespace 'Shop.Web'");
    }

    [Fact]
    public void Conditional_properties_are_ignored()
    {
        using var project = new TempProject();
        var dir = Path.Combine(project.Root, "Shop.Web");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "Shop.Web.csproj"),
            "<Project><PropertyGroup Condition=\"'$(X)'=='1'\"><AssemblyName>Shop.Domain</AssemblyName></PropertyGroup>"
            + "<PropertyGroup><RootNamespace Condition=\"'$(X)'=='1'\">Other</RootNamespace></PropertyGroup></Project>");

        NewFilePlacement.Check(Pack, project.Root, Path.Combine(dir, "A.cs"), null)
            .Should().Contain("assembly 'Shop.Web'").And.Contain("namespace 'Shop.Web'");
    }

    [Fact]
    public void A_commented_namespace_line_is_not_a_declaration()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Agents", "Shop.Agents");
        var file = Path.Combine(project.Root, "Shop.Agents", "A.cs");

        NewFilePlacement.Check(Pack, project.Root, file, "// namespace Shop.Agents.Runtime.X;\nclass A;")
            .Should().Contain("namespace 'Shop.Agents'");
    }

    [Fact]
    public void With_several_namespace_declarations_the_first_wins()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Agents", "Shop.Agents");
        var file = Path.Combine(project.Root, "Shop.Agents", "A.cs");

        NewFilePlacement.Check(Pack, project.Root, file, "namespace Shop.Agents.Runtime.A\n{\n}\nnamespace Elsewhere.B\n{\n}")
            .Should().BeNull();
        NewFilePlacement.Check(Pack, project.Root, file, "namespace Elsewhere.B\n{\n}\nnamespace Shop.Agents.Runtime.A\n{\n}")
            .Should().Contain("namespace 'Elsewhere.B'");
    }

    [Theory]
    [InlineData("namespace   Shop.Agents.Runtime.A   ;")]
    [InlineData("\tnamespace\tShop.Agents.Runtime.A\n;")]
    [InlineData("namespace Shop.Agents.Runtime.A{}")]
    public void Unusual_spacing_still_finds_the_namespace(string content)
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Agents", "Shop.Agents");

        NewFilePlacement.Check(Pack, project.Root, Path.Combine(project.Root, "Shop.Agents", "A.cs"), content).Should().BeNull();
    }

    [Fact]
    public void A_global_qualified_name_in_the_content_is_not_a_namespace_declaration()
    {
        using var project = new TempProject();
        Csproj(project, "Shop.Agents", "Shop.Agents");

        NewFilePlacement.Check(Pack, project.Root, Path.Combine(project.Root, "Shop.Agents", "A.cs"), "using X = global::Shop.Agents.Runtime;\nclass A;")
            .Should().Contain("namespace 'Shop.Agents'");
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

    // The files that were evaluated are those below a project and outside obj and bin, counted independently of the code under test.
    private (List<string> Unplaced, List<string> Evaluated) Evaluate(RulePack pack, string root, string? sub = null)
    {
        var unplaced = new List<string>();
        var evaluated = new List<string>();
        var start = sub is null ? root : Path.Combine(root, sub);
        foreach (var file in Directory.EnumerateFiles(start, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var segments = relative.Split('/');
            if (Array.IndexOf(segments, "obj") >= 0 || Array.IndexOf(segments, "bin") >= 0 || !HasProjectAbove(root, file))
            {
                continue;
            }

            evaluated.Add(relative);
            var question = NewFilePlacement.Check(pack, root, file, File.ReadAllText(file), treatAsNew: true);
            if (question is not null)
            {
                output.WriteLine(question);
                unplaced.Add(relative);
            }
        }

        return (unplaced, evaluated);
    }

    private static bool HasProjectAbove(string root, string file)
    {
        var dir = Path.GetDirectoryName(file);
        while (dir is not null && dir.StartsWith(root, StringComparison.Ordinal))
        {
            if (Directory.GetFiles(dir, "*.csproj").Length > 0)
            {
                return true;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return false;
    }
}
