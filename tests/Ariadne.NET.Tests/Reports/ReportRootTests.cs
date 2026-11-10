using Ariadne.NET.Tests.Shared;
using Ariadne.NET.Reports;
using FluentAssertions;
using Xunit;

namespace Ariadne.NET.Tests.Reports;

public sealed class ReportRootTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "ariadne-root-" + Guid.NewGuid().ToString("N"));

    public ReportRootTests() => Directory.CreateDirectory(_temp);

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private string Folder(params string[] parts)
    {
        var path = Path.Combine([_temp, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void Find_ReturnsTheAncestorHoldingAGitFolder()
    {
        var repo = Folder("repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var solution = Folder("repo", "src", "App");

        ReportRoot.Find(solution).Should().Be(repo);
    }

    [Fact]
    public void Find_ReturnsTheAncestorHoldingAGitFile()
    {
        var repo = Folder("worktree");
        File.WriteAllText(Path.Combine(repo, ".git"), "gitdir: elsewhere");
        var solution = Folder("worktree", "src");

        ReportRoot.Find(solution).Should().Be(repo);
    }

    [Fact]
    public void Find_ReturnsTheSolutionFolderItselfWhenItHoldsGit()
    {
        var repo = Folder("repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));

        ReportRoot.Find(repo).Should().Be(repo);
    }

    [Fact]
    public void Find_PrefersTheNearestGitEntry()
    {
        var outer = Folder("outer");
        Directory.CreateDirectory(Path.Combine(outer, ".git"));
        var inner = Folder("outer", "sub");
        File.WriteAllText(Path.Combine(inner, ".git"), "gitdir: x");
        var solution = Folder("outer", "sub", "App");

        ReportRoot.Find(solution).Should().Be(inner);
    }

    [Fact]
    public void Find_FallsBackToTheSolutionFolderWithoutGit()
    {
        var solution = Folder("plain", "App");
        StrayFiles.AssertNoGitAbove(solution);

        ReportRoot.Find(solution).Should().Be(solution);
    }

    [Fact]
    public void Find_ReturnsAFullPathForARelativeInput()
    {
        var solution = Folder("plain");
        StrayFiles.AssertNoGitAbove(solution);
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), solution);

        ReportRoot.Find(relative).Should().Be(solution);
    }

    [Fact]
    public void Find_RejectsNullAndEmpty()
    {
        FluentActions.Invoking(() => ReportRoot.Find(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ReportRoot.Find("")).Should().Throw<ArgumentException>();
    }
}
