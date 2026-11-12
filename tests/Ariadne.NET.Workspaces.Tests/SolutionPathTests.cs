using Ariadne.NET.Abstractions;
using Ariadne.NET.Tests.Shared;
using FluentAssertions;

namespace Ariadne.NET.Workspaces.Tests;

[Collection(FixtureSolutionCollection.Name)]
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
        var held = FixtureProjects.TakeLock(FixtureProjects.Path(FixtureProjects.LockFileName));
        Task restore;
        try
        {
            restore = Task.Run(() => FixtureProjects.RestoreUnderLock(FixtureProjects.Root));

            await Task.Delay(300);
            restore.IsCompleted.Should().BeFalse("the lock is held by someone else");
        }
        finally
        {
            await held.DisposeAsync();
        }

        await restore.WaitAsync(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void RestoreUnderLock_MissingAssetsFile_RestoresAgain()
    {
        FixtureProjects.EnsureRestored();
        var copy = Directory.CreateDirectory(Path.Combine(_dir, "fixtures")).FullName;
        CopySources(FixtureProjects.Root, copy);
        var assets = Path.Combine(copy, "multitarget", "obj", "project.assets.json");

        FixtureProjects.RestoreUnderLock(copy);
        File.Exists(assets).Should().BeTrue();
        FixtureProjects.OutputsExist(copy).Should().BeTrue();

        File.Delete(assets);
        FixtureProjects.OutputsExist(copy).Should().BeFalse();

        FixtureProjects.RestoreUnderLock(copy);

        File.Exists(assets).Should().BeTrue();
        FixtureProjects.OutputsExist(copy).Should().BeTrue();
    }

    [Fact]
    public void SourceStamp_ChangesOnDeleteAndRename_NotOnBuildOutput()
    {
        var root = Directory.CreateDirectory(Path.Combine(_dir, "stamp")).FullName;
        var a = Path.Combine(root, "A.cs");
        var b = Path.Combine(root, "B.cs");
        File.WriteAllText(a, "a");
        File.WriteAllText(b, "b");
        // The newest write is the same throughout, so only the file list can change the stamp.
        var newest = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(a, newest.AddMinutes(-1));
        File.SetLastWriteTimeUtc(b, newest);
        var original = FixtureProjects.SourceStamp(root);

        Directory.CreateDirectory(Path.Combine(root, "bin"));
        File.WriteAllText(Path.Combine(root, "bin", "A.dll"), "dll");
        File.WriteAllText(Path.Combine(root, ".parity-build-marker"), "marker");
        FixtureProjects.SourceStamp(root).Should().Be(original);

        var renamed = Path.Combine(root, "C.cs");
        File.Move(a, renamed);
        var afterRename = FixtureProjects.SourceStamp(root);
        afterRename.Should().NotBe(original);

        File.Delete(renamed);
        FixtureProjects.SourceStamp(root).Should().NotBe(original).And.NotBe(afterRename);
    }

    [Fact]
    public void IsSharingViolation_TrueForAHeldLock()
    {
        var lockPath = Path.Combine(_dir, "held.lock");
        using var held = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var act = () => new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var thrown = act.Should().Throw<IOException>().Which;
        FixtureProjects.IsSharingViolation(thrown).Should().BeTrue();
    }

    [Fact]
    public void IsSharingViolation_FalseForOtherIOExceptions()
    {
        FixtureProjects.IsSharingViolation(new IOException("wrapped", new FileNotFoundException("gone"))).Should().BeFalse();
        FixtureProjects.IsSharingViolation(new DirectoryNotFoundException("gone")).Should().BeFalse();
        FixtureProjects.IsSharingViolation(new IOException("There is not enough space on the disk.", unchecked((int)0x80070070))).Should().BeFalse();
    }

    // Copies the fixture sources, keeping the fixture-local Directory.Build files so the import chain still stops there. The
    // ArchUnitNET parity fixtures are left out: no restore target lies under them.
    private static void CopySources(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, file);
            var segments = relative.Split(Path.DirectorySeparatorChar);
            if (string.Equals(segments[0], "archunit-parity", StringComparison.Ordinal)
                || Array.Exists(segments, s => string.Equals(s, "bin", StringComparison.Ordinal) || string.Equals(s, "obj", StringComparison.Ordinal))
                || FixtureProjects.IsLockOrMarker(segments[^1]))
            {
                continue;
            }

            var target = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private string Touch(string name)
    {
        var file = Path.Combine(_dir, name);
        File.WriteAllText(file, string.Empty);
        return file;
    }
}
