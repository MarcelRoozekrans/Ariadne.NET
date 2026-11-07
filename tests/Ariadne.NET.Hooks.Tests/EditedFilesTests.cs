using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class EditedFilesTests
{
    private static string NewSession() => Guid.NewGuid().ToString("N");

    [Fact]
    public void Records_deduplicates_reads_and_clears()
    {
        var session = NewSession();
        EditedFiles.Record(session, "/a/One.cs");
        EditedFiles.Record(session, "/a/Two.cs");
        EditedFiles.Record(session, "/a/One.cs");

        EditedFiles.Read(session).Should().Equal("/a/One.cs", "/a/Two.cs");

        EditedFiles.Clear(session);
        EditedFiles.Read(session).Should().BeEmpty();
    }

    [Fact]
    public void Paths_differing_in_case_are_different_entries()
    {
        var session = NewSession();
        EditedFiles.Record(session, "/a/one.cs");
        EditedFiles.Record(session, "/a/One.cs");

        EditedFiles.Read(session).Should().HaveCount(2);
        EditedFiles.Clear(session);
    }

    [Fact]
    public void Sessions_are_separate()
    {
        var first = NewSession();
        var second = NewSession();
        EditedFiles.Record(first, "/a/One.cs");

        EditedFiles.Read(second).Should().BeEmpty();
        EditedFiles.Clear(first);
    }

    [Fact]
    public void Reading_a_session_without_a_file_is_empty() =>
        EditedFiles.Read(NewSession()).Should().BeEmpty();

    [Theory]
    [InlineData("../x")]
    [InlineData("..\\x")]
    [InlineData("")]
    [InlineData("a/b/../../../c")]
    public void Hostile_session_ids_stay_inside_the_folder(string session)
    {
        var folder = Path.Combine(Path.GetTempPath(), "ariadne-hooks");
        var before = Directory.Exists(folder) ? Directory.GetFiles(folder).Length : 0;

        EditedFiles.Record(session, "/a/One.cs");

        EditedFiles.Read(session).Should().Equal("/a/One.cs");
        Directory.GetFiles(folder).Length.Should().BeGreaterThanOrEqualTo(before);
        File.Exists(Path.Combine(Path.GetTempPath(), "x")).Should().BeFalse();
        EditedFiles.Clear(session);
        EditedFiles.Read(session).Should().BeEmpty();
    }
}
