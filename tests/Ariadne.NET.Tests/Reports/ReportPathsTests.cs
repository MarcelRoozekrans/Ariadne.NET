using FluentAssertions;
using Ariadne.NET.Reports;

namespace Ariadne.NET.Tests.Reports;

public sealed class ReportPathsTests
{
    [Theory]
    [InlineData("/home/dev/my repo/", "file:///home/dev/my%20repo/")]
    [InlineData("/home/dev/a#b%c/x.cs", "file:///home/dev/a%23b%25c/x.cs")]
    [InlineData("C:/Users/dev/repo/", "file:///C:/Users/dev/repo/")]
    [InlineData("C:/Users/dev/a b#1/x.cs", "file:///C:/Users/dev/a%20b%231/x.cs")]
    [InlineData("//host/share/x y.cs", "file://host/share/x%20y.cs")]
    public void FileUriFromNormalized_EscapesEachSegment(string path, string expected) =>
        ReportPaths.FileUriFromNormalized(path).Should().Be(expected);
}
