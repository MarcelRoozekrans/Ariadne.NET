using System.Runtime.Versioning;
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
        // Many rounds of writers released by a barrier, so the opens really overlap; each round is a fresh session.
        for (var round = 0; round < 40; round++)
        {
            RecordConcurrently(round);
        }
    }

    private static void RecordConcurrently(int round)
    {
        const int writers = 16;
        using var session = new TestSession();
        using var barrier = new Barrier(writers);
        var threads = new Thread[writers];
        var expected = new string[writers];
        for (var i = 0; i < writers; i++)
        {
            var path = $"/a/File{i}.cs";
            expected[i] = path;
            threads[i] = new Thread(() =>
            {
                barrier.SignalAndWait();
                EditedFiles.Record(session.Id, path);
            });
            threads[i].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        EditedFiles.Read(session.Id).Should().BeEquivalentTo(expected, $"round {round}");
    }

    [Fact]
    public void A_record_made_between_read_and_clear_survives_removing_what_was_read()
    {
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/a/One.cs");
        var snapshot = EditedFiles.ReadRecords(session.Id);

        EditedFiles.Record(session.Id, "/a/Two.cs");
        EditedFiles.Remove(snapshot);

        EditedFiles.Read(session.Id).Should().Equal("/a/Two.cs");
    }

    [Fact]
    public void Clearing_a_record_that_is_open_for_reading_does_not_fail()
    {
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/a/One.cs");
        var file = Directory.GetFiles(EditedFiles.SessionFolder(session.Id)).First();

        using var open = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var act = () => EditedFiles.Clear(session.Id);

        act.Should().NotThrow();
    }

    [Fact]
    public void Stale_session_folders_are_pruned_and_fresh_ones_kept()
    {
        using var stale = new TestSession();
        using var fresh = new TestSession();
        EditedFiles.Record(stale.Id, "/a/One.cs");
        EditedFiles.Record(fresh.Id, "/a/Two.cs");
        Directory.SetLastWriteTimeUtc(EditedFiles.SessionFolder(stale.Id), DateTime.UtcNow - TimeSpan.FromDays(8));

        EditedFiles.Prune(TimeSpan.FromDays(7));

        Directory.Exists(EditedFiles.SessionFolder(stale.Id)).Should().BeFalse();
        EditedFiles.Read(fresh.Id).Should().Equal("/a/Two.cs");
    }

    [Fact]
    public void Recording_prunes_stale_session_folders_without_failing()
    {
        using var stale = new TestSession();
        EditedFiles.Record(stale.Id, "/a/One.cs");
        Directory.SetLastWriteTimeUtc(EditedFiles.SessionFolder(stale.Id), DateTime.UtcNow - TimeSpan.FromDays(30));
        using var other = new TestSession();

        EditedFiles.Record(other.Id, "/a/Two.cs");

        Directory.Exists(EditedFiles.SessionFolder(stale.Id)).Should().BeFalse();
    }

    [UnixFact]
    [UnsupportedOSPlatform("windows")]
    public void The_hook_folder_is_private_to_the_user()
    {
        using var session = new TestSession();
        EditedFiles.Record(session.Id, "/a/One.cs");

        File.GetUnixFileMode(EditedFiles.Folder).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.GetUnixFileMode(EditedFiles.SessionFolder(session.Id)).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
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
        var folder = EditedFiles.SessionFolder(id);

        try
        {
            EditedFiles.Record(id, "/a/One.cs");

            Path.GetDirectoryName(folder).Should().Be(EditedFiles.Folder);
            Directory.Exists(folder).Should().BeTrue();
            EditedFiles.Read(id).Should().Equal("/a/One.cs");
            // Nothing leaked out of the folder into the temp root or above it.
            File.Exists(Path.Combine(Path.GetTempPath(), "x")).Should().BeFalse();
            File.Exists(Path.Combine(Path.GetTempPath(), "..", "x.txt")).Should().BeFalse();
        }
        finally
        {
            EditedFiles.Clear(id);
        }
    }
}
