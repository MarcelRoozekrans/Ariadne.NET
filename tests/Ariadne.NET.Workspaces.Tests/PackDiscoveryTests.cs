using Ariadne.NET.Abstractions;
using FluentAssertions;

namespace Ariadne.NET.Workspaces.Tests;

public sealed class PackDiscoveryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ariadne-discovery-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Files_AreSortedByOrdinalFileName()
    {
        var solution = Touch("Shop.slnx");
        Touch(".architecture/b.yaml");
        Touch(".architecture/B.yaml");
        Touch(".architecture/a.yaml");

        var result = PackDiscovery.Find(solution);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectRoot.Should().Be(_root);
        result.Value.Files.Select(Path.GetFileName).Should().Equal("B.yaml", "a.yaml", "b.yaml");
        result.Value.Files.Should().OnlyContain(f => Path.IsPathRooted(f));
    }

    [Fact]
    public void YmlIsAccepted_AndOtherExtensionsAreIgnored()
    {
        var solution = Touch("Shop.slnx");
        Touch(".architecture/one.yml");
        Touch(".architecture/two.yaml");
        Touch(".architecture/notes.txt");

        var result = PackDiscovery.Find(solution);

        result.Value.Files.Select(Path.GetFileName).Should().Equal("one.yml", "two.yaml");
    }

    [Fact]
    public void SubfoldersAndCache_AreNotScanned()
    {
        var solution = Touch("Shop.slnx");
        Touch(".architecture/rules.yaml");
        Touch(".architecture/.cache/git/x/pack.yaml");
        Touch(".architecture/nested/other.yaml");

        var result = PackDiscovery.Find(solution);

        result.Value.Files.Select(Path.GetFileName).Should().Equal("rules.yaml");
    }

    [Fact]
    public void NestedSolution_WalksUpToNearestArchitectureFolder()
    {
        Touch(".architecture/outer.yaml");
        Touch("sub/.architecture/inner.yaml");
        var solution = Touch("sub/deep/Shop.slnx");

        var result = PackDiscovery.Find(solution);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectRoot.Should().Be(Path.Combine(_root, "sub"));
        result.Value.Files.Select(Path.GetFileName).Should().Equal("inner.yaml");
    }

    [Fact]
    public void DirectoryPath_IsResolvedToItsSolution()
    {
        Touch("Shop.slnx");
        Touch(".architecture/rules.yaml");

        var result = PackDiscovery.Find(_root);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProjectRoot.Should().Be(_root);
    }

    [Fact]
    public void NoArchitectureFolder_IsNotFound()
    {
        var solution = Touch("Shop.slnx");
        for (var dir = new DirectoryInfo(_root); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".architecture")))
            {
                return; // the machine has an .architecture folder above the temp folder, so this case cannot be set up here
            }
        }

        var result = PackDiscovery.Find(solution);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
        result.Error.Message.Should().EndWith(".").And.Contain(_root);
    }

    [Fact]
    public void EmptyArchitectureFolder_IsNotFound()
    {
        var solution = Touch("Shop.slnx");
        Directory.CreateDirectory(Path.Combine(_root, ".architecture"));

        var result = PackDiscovery.Find(solution);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
        result.Error.Message.Should().EndWith(".").And.Contain(Path.Combine(_root, ".architecture"));
    }

    [Fact]
    public void MissingPath_PropagatesSolutionPathError()
    {
        var result = PackDiscovery.Find(Path.Combine(_root, "missing"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
    }

    private string Touch(string relative)
    {
        var file = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, string.Empty);
        return file;
    }
}
