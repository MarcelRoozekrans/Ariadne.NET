using Ariadne.NET.Abstractions;
using FluentAssertions;

namespace Ariadne.NET.Testing.Tests;

public sealed class SolutionLocatorTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("ariadne-locator-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void Find_ReturnsTheSolutionInTheNearestAncestorFolder()
    {
        File.WriteAllText(Path.Combine(_temp, "Upper.sln"), "");
        var nearest = Directory.CreateDirectory(Path.Combine(_temp, "repo")).FullName;
        File.WriteAllText(Path.Combine(nearest, "App.slnx"), "<Solution />");
        File.WriteAllText(Path.Combine(nearest, "App.csproj"), "<Project />");
        var start = Directory.CreateDirectory(Path.Combine(nearest, "tests", "bin", "Debug")).FullName;

        var result = SolutionLocator.Find(start);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
        result.Value.Should().Be(Path.Combine(nearest, "App.slnx"));
    }

    [Fact]
    public void Find_SolutionInTheStartFolder_IsFound()
    {
        File.WriteAllText(Path.Combine(_temp, "App.sln"), "");

        var result = SolutionLocator.Find(_temp);

        result.Value.Should().Be(Path.Combine(_temp, "App.sln"));
    }

    [Fact]
    public void Find_NoSolutionAnywhereAbove_IsNotFound()
    {
        var start = Directory.CreateDirectory(Path.Combine(_temp, "a", "b")).FullName;

        var result = SolutionLocator.Find(start);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.NotFound);
        result.Error.Message.Should().Contain(start).And.Contain(".sln").And.EndWith(".");
    }

    [Fact]
    public void Find_TwoSolutionsInTheNearestFolder_IsInvalid_AndNamesBoth()
    {
        File.WriteAllText(Path.Combine(_temp, "Upper.sln"), "");
        var nearest = Directory.CreateDirectory(Path.Combine(_temp, "repo")).FullName;
        File.WriteAllText(Path.Combine(nearest, "App.sln"), "");
        File.WriteAllText(Path.Combine(nearest, "App.slnx"), "<Solution />");

        var result = SolutionLocator.Find(nearest);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(AriadneErrorCode.Invalid);
        result.Error.Message.Should().Contain(nearest).And.EndWith(".");
        result.Error.Detail.Should().Contain("App.sln").And.Contain("App.slnx").And.NotContain("Upper.sln");
    }
}
