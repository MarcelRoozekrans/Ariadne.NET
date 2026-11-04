using Ariadne.NET.Abstractions;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;

namespace Ariadne.NET.Workspaces.Tests;

public sealed class SolutionPathTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ariadne-path-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("Shop.sln")]
    [InlineData("Shop.slnx")]
    [InlineData("Shop.slnf")]
    [InlineData("Shop.csproj")]
    public void SolutionFile_ReturnsFullPath(string name)
    {
        var file = Touch(name);

        var result = SolutionPath.Resolve(file);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(file);
    }

    [Fact]
    public void DirectoryWithOneSolution_ReturnsIt()
    {
        var file = Touch("Shop.slnx");
        Touch("notes.txt");

        var result = SolutionPath.Resolve(_dir);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(file);
    }

    [Fact]
    public void DirectoryWithTwo_IsInvalidAndListsBoth()
    {
        Touch("A.sln");
        Touch("B.csproj");

        var result = SolutionPath.Resolve(_dir);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain(_dir).And.EndWith(".");
        result.Error.Detail.Should().Contain("A.sln").And.Contain("B.csproj");
    }

    [Fact]
    public void EmptyDirectory_IsInvalid()
    {
        var result = SolutionPath.Resolve(_dir);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain(_dir).And.EndWith(".");
    }

    [Fact]
    public void MissingPath_IsNotFound()
    {
        var result = SolutionPath.Resolve(Path.Combine(_dir, "missing.sln"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
        result.Error.Message.Should().EndWith(".");
    }

    [Fact]
    public void OtherExtension_IsInvalid()
    {
        var result = SolutionPath.Resolve(Touch("Program.cs"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().EndWith(".");
    }

    [Fact]
    public void RelativePath_ResolvesAgainstCurrentDirectory()
    {
        var file = Touch("Shop.slnx");
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), file);

        var result = SolutionPath.Resolve(relative);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(file);
    }

    [Fact]
    public void PathWithSpaces_Resolves()
    {
        var spaced = Directory.CreateDirectory(Path.Combine(_dir, "my shop")).FullName;
        var file = Path.Combine(spaced, "My Shop.slnx");
        File.WriteAllText(file, string.Empty);

        var result = SolutionPath.Resolve(file);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(file);
    }

    [Fact]
    public void SlnAndSlnxTogether_IsInvalidAndListsBoth()
    {
        Touch("Shop.sln");
        Touch("Shop.slnx");

        var result = SolutionPath.Resolve(_dir);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Detail.Should().Contain("Shop.sln,").And.Contain("Shop.slnx");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\0b.sln")]
    public void EmptyOrMalformedPath_IsInvalid(string path)
    {
        var result = SolutionPath.Resolve(path);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().EndWith(".");
    }

    [Fact]
    public async Task RestoreUnderLock_WaitsWhileAnotherHolderHasTheLock()
    {
        FixtureProjects.EnsureRestored();
        var lockPath = FixtureProjects.Path(".restore.lock");
        var held = FixtureProjects.TakeLock(lockPath);
        Task restore;
        try
        {
            restore = Task.Run(FixtureProjects.RestoreUnderLock);

            await Task.Delay(300);
            restore.IsCompleted.Should().BeFalse("the lock is held by someone else");
        }
        finally
        {
            await held.DisposeAsync();
        }

        await restore.WaitAsync(TimeSpan.FromMinutes(2));
        File.Exists(FixtureProjects.Path("clean", "Shop.Domain", "obj", "project.assets.json")).Should().BeTrue();
    }

    [Fact]
    public void RestoreUnderLock_MissingAssetsFile_RestoresAgain()
    {
        FixtureProjects.EnsureRestored();
        var assets = FixtureProjects.Path("multitarget", "obj", "project.assets.json");
        File.Delete(assets);
        FixtureProjects.OutputsExist().Should().BeFalse();

        FixtureProjects.RestoreUnderLock();

        File.Exists(assets).Should().BeTrue();
        FixtureProjects.OutputsExist().Should().BeTrue();
    }

    private string Touch(string name)
    {
        var file = Path.Combine(_dir, name);
        File.WriteAllText(file, string.Empty);
        return file;
    }
}
