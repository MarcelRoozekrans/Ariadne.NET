using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class EditedFilesTests
{
    [Fact]
    public void Records_deduplicates_reads_and_clears()
    {
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/a/One.cs");
        EditedFiles.Record(session.Id, "/a/Two.cs");
        EditedFiles.Record(session.Id, "/a/One.cs");

        EditedFiles.Read(session.Id).Should().Equal("/a/One.cs", "/a/Two.cs");

        EditedFiles.Clear(session.Id);
        EditedFiles.Read(session.Id).Should().BeEmpty();
    }

    [Fact]
    public void Paths_differing_in_case_are_different_entries()
    {
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/a/one.cs");
        EditedFiles.Record(session.Id, "/a/One.cs");

        EditedFiles.Read(session.Id).Should().HaveCount(2);
    }

    [Fact]
    public void Sessions_are_separate()
    {
        using var first = new TestSession();
        using var second = new TestSession();
        EditedFiles.Record(first.Id, "/a/One.cs");

        EditedFiles.Read(second.Id).Should().BeEmpty();
    }

    [Fact]
    public void Reading_a_session_without_a_file_is_empty()
    {
        using var session = new TestSession();
        EditedFiles.Read(session.Id).Should().BeEmpty();
    }

    [Fact]
    public void Concurrent_records_all_land()
    {
        using var session = new TestSession();

        Parallel.For(0, 40, i => EditedFiles.Record(session.Id, $"/a/File{i % 20}.cs"));

        EditedFiles.Read(session.Id).Should().HaveCount(20);
    }

    [Theory]
    [InlineData("../x", "___x")]
    [InlineData("..\\x", "___x")]
    [InlineData("", "_")]
    [InlineData("abc-DEF_123", "abc-DEF_123")]
    [InlineData("a/b/../../../c", "a_b__________c")]
    public void Session_ids_are_reduced_to_a_safe_file_name(string id, string expected) =>
        EditedFiles.SafeName(id).Should().Be(expected);

    [Fact]
    public void Distinct_ids_may_share_a_file()
    {
        // By design: the id is sanitised, not hashed, so these two collide.
        EditedFiles.SafeName("a/b").Should().Be(EditedFiles.SafeName("a_b"));
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("..\\x")]
    [InlineData("")]
    public void Hostile_session_ids_stay_inside_the_hook_folder(string id)
    {
        var path = EditedFiles.PathOf(id);

        try
        {
            EditedFiles.Record(id, "/a/One.cs");

            Path.GetDirectoryName(path).Should().Be(Path.Combine(Path.GetTempPath(), "ariadne-hooks"));
            File.Exists(path).Should().BeTrue();
            EditedFiles.Read(id).Should().Equal("/a/One.cs");
            // Nothing leaked out of the folder into the temp root or above it.
            File.Exists(Path.Combine(Path.GetTempPath(), "x")).Should().BeFalse();
            File.Exists(Path.Combine(Path.GetTempPath(), "x.txt")).Should().BeFalse();
            File.Exists(Path.Combine(Path.GetTempPath(), "..", "x.txt")).Should().BeFalse();
        }
        finally
        {
            EditedFiles.Clear(id);
        }
    }
}
