using System.Text.Json.Nodes;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Cli;
using FluentAssertions;

namespace Ariadne.NET.Cli.Tests;

public sealed class AgentSettingsTests
{
    private const string Prefix = AgentSettings.HookCommandPrefix;

    // Output is read before any assertion runs: FluentAssertions prints a licence banner to Console.Out on first use.
    private static string MergeOk(string? existing)
    {
        var result = AgentSettings.Merge(existing);
        result.IsSuccess.Should().BeTrue(result.IsSuccess ? "" : result.Error.Message);
        return result.Value;
    }

    private static JsonObject Parse(string text) => (JsonObject)JsonNode.Parse(text)!;

    private static JsonArray Groups(JsonObject root, string evt) => (JsonArray)root["hooks"]![evt]!;

    private static IEnumerable<JsonObject> Entries(JsonArray groups) =>
        groups.SelectMany(g => ((JsonArray)g!["hooks"]!).Select(e => (JsonObject)e!));

    private static JsonObject OnlyEntry(JsonArray groups)
    {
        var all = Entries(groups).ToList();
        all.Should().HaveCount(1);
        return all.First();
    }

    private static List<string> Commands(JsonArray groups) =>
        Entries(groups).Select(e => (string)e["command"]!).ToList();

    [Fact]
    public void Merge_from_null_writes_the_three_entries_with_timeouts()
    {
        var root = Parse(MergeOk(null));

        var start = Groups(root, "SessionStart");
        start.Should().HaveCount(1);
        start[0]!["matcher"].Should().BeNull();
        var s = OnlyEntry(start);
        ((string)s["type"]!).Should().Be("command");
        ((string)s["command"]!).Should().Be($"{Prefix} session-start");
        ((int)s["timeout"]!).Should().Be(30);

        var pre = Groups(root, "PreToolUse");
        ((string)pre[0]!["matcher"]!).Should().Be("Write|Edit|MultiEdit");
        var p = OnlyEntry(pre);
        ((string)p["command"]!).Should().Be($"{Prefix} pre-tool-use");
        ((int)p["timeout"]!).Should().Be(10);

        var stop = Groups(root, "Stop");
        stop[0]!["matcher"].Should().BeNull();
        var st = OnlyEntry(stop);
        ((string)st["command"]!).Should().Be($"{Prefix} stop");
        ((int)st["timeout"]!).Should().Be(300);
    }

    [Fact]
    public void Merge_uses_two_space_indentation_and_ends_with_a_newline()
    {
        var text = MergeOk(null);

        text.Should().StartWith("{\n  \"hooks\": {\n    \"SessionStart\"");
        text.Should().EndWith("}\n");
    }

    [Fact]
    public void Merge_keeps_other_keys()
    {
        var root = Parse(MergeOk("""{"permissions":{"allow":["Bash(ls)"]}}"""));

        ((string)root["permissions"]!["allow"]![0]!).Should().Be("Bash(ls)");
        root["hooks"].Should().NotBeNull();
    }

    [Fact]
    public void Merge_keeps_a_foreign_Stop_entry_before_ours()
    {
        var root = Parse(MergeOk("""{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"echo done"}]}]}}"""));

        Commands(Groups(root, "Stop")).Should().Equal("echo done", $"{Prefix} stop");
    }

    [Fact]
    public void Merge_rewrites_a_stale_entry_instead_of_duplicating_it()
    {
        var root = Parse(MergeOk(
            """{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"ariadne hook stop --old","timeout":5,"extra":1}]}]}}"""));

        var entry = OnlyEntry(Groups(root, "Stop"));
        ((string)entry["command"]!).Should().Be($"{Prefix} stop");
        ((int)entry["timeout"]!).Should().Be(5);
        ((int)entry["extra"]!).Should().Be(1);
    }

    [Fact]
    public void Merge_preserves_a_custom_timeout_on_an_existing_entry()
    {
        var root = Parse(MergeOk(
            """{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"ariadne hook stop","timeout":900}]}]}}"""));

        ((int)OnlyEntry(Groups(root, "Stop"))["timeout"]!).Should().Be(900);
    }

    [Theory]
    [InlineData("""{"type":"command","command":"ariadne hook stop"}""")]
    [InlineData("""{"type":"command","command":"ariadne hook stop","timeout":null}""")]
    [InlineData("""{"type":"command","command":"ariadne hook stop","timeout":"slow"}""")]
    [InlineData("""{"type":"command","command":"ariadne hook stop","timeout":0}""")]
    [InlineData("""{"type":"command","command":"ariadne hook stop","timeout":-5}""")]
    public void Merge_fills_a_missing_or_unusable_timeout(string entryJson)
    {
        var root = Parse(MergeOk("{\"hooks\":{\"Stop\":[{\"hooks\":[" + entryJson + "]}]}}"));

        ((int)OnlyEntry(Groups(root, "Stop"))["timeout"]!).Should().Be(300);
    }

    [Fact]
    public void Merge_removes_duplicate_Ariadne_entries()
    {
        var root = Parse(MergeOk(
            """{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"ariadne hook stop"},{"type":"command","command":"keep"},{"type":"command","command":"ariadne hook stop"}]}]}}"""));

        Commands(Groups(root, "Stop")).Should().Equal($"{Prefix} stop", "keep");
    }

    [Fact]
    public void Merge_is_byte_identical_on_its_own_output()
    {
        var first = MergeOk("""{"a":1,"hooks":{"Stop":[{"hooks":[{"type":"command","command":"x"}]}]}}""");

        MergeOk(first).Should().Be(first);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("{ // comment\n \"a\": 1 }")]
    [InlineData("{\"a\": 1,}")]
    [InlineData("""{"a":1,"a":2}""")]
    [InlineData("""{"hooks":{"Stop":[],"Stop":[]}}""")]
    [InlineData("""{"hooks":null}""")]
    [InlineData("""{"hooks":[]}""")]
    [InlineData("""{"hooks":{"Stop":"x"}}""")]
    [InlineData("""{"hooks":{"Stop":null}}""")]
    [InlineData("""{"hooks":{"Stop":[{"hooks":"x"}]}}""")]
    public void Merge_rejects_text_it_cannot_merge_safely(string existing)
    {
        var result = AgentSettings.Merge(existing);

        var failed = result.IsFailure;
        var code = failed ? result.Error.Code : default;
        var message = failed ? result.Error.Message : "";
        failed.Should().BeTrue();
        code.Should().Be(AriadneErrorCode.Invalid);
        message.Should().EndWith(".");
    }

    [Fact]
    public void Merge_moves_our_command_out_of_a_group_with_a_different_matcher()
    {
        var root = Parse(MergeOk(
            """{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"ariadne hook pre-tool-use"},{"type":"command","command":"other"}]}]}}"""));

        var groups = Groups(root, "PreToolUse");
        groups.Should().HaveCount(2);
        ((string)groups[0]!["matcher"]!).Should().Be("Bash");
        Commands(new JsonArray(groups[0]!.DeepClone())).Should().Equal("other");
        ((string)groups[1]!["matcher"]!).Should().Be("Write|Edit|MultiEdit");
        Commands(new JsonArray(groups[1]!.DeepClone())).Should().Equal($"{Prefix} pre-tool-use");
    }

    [Fact]
    public void Merge_drops_a_group_that_only_held_our_command_under_another_matcher()
    {
        var root = Parse(MergeOk(
            """{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"ariadne hook pre-tool-use"}]}]}}"""));

        var groups = Groups(root, "PreToolUse");
        groups.Should().HaveCount(1);
        ((string)groups[0]!["matcher"]!).Should().Be("Write|Edit|MultiEdit");
    }

    [Fact]
    public void Merge_appends_to_an_existing_group_with_our_matcher()
    {
        var root = Parse(MergeOk(
            """{"hooks":{"PreToolUse":[{"matcher":"Write|Edit|MultiEdit","hooks":[{"type":"command","command":"lint"}]}]}}"""));

        var groups = Groups(root, "PreToolUse");
        groups.Should().HaveCount(1);
        Commands(groups).Should().Equal("lint", $"{Prefix} pre-tool-use");
    }

    [Fact]
    public void Merge_preserves_non_ascii_and_quotes_in_foreign_entries()
    {
        var text = MergeOk("""{"note":"café it's <ok> & ✓","hooks":{"Stop":[{"hooks":[{"type":"command","command":"echo 'héllo ✓'"}]}]}}""");

        text.Should().Contain("café it's <ok> & ✓");
        text.Should().Contain("echo 'héllo ✓'");
    }

    [Fact]
    public void Merge_tolerates_a_byte_order_mark()
    {
        var root = Parse(MergeOk("\uFEFF{\"a\":1}"));

        ((int)root["a"]!).Should().Be(1);
    }

    [Fact]
    public void Merge_treats_blank_text_as_an_empty_object()
    {
        Parse(MergeOk("  \n")).ContainsKey("hooks").Should().BeTrue();
    }

    [Fact]
    public void Merge_does_not_treat_a_similar_prefix_as_ours()
    {
        var root = Parse(MergeOk(
            """{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"ariadne hooks-other"}]}]}}"""));

        Commands(Groups(root, "Stop")).Should().Equal("ariadne hooks-other", $"{Prefix} stop");
    }

    [Theory]
    [InlineData("/usr/bin/ariadne hook stop")]
    [InlineData("  ariadne hook stop --old")]
    [InlineData("C:\\tools\\ariadne.exe hook stop")]
    public void Merge_recognises_a_path_qualified_or_padded_command_as_ours(string command)
    {
        var json = new JsonObject { ["hooks"] = new JsonObject { ["Stop"] = new JsonArray(new JsonObject
        {
            ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = command }),
        }) } }.ToJsonString();

        var root = Parse(MergeOk(json));

        Commands(Groups(root, "Stop")).Should().Equal($"{Prefix} stop");
    }

    [Fact]
    public void Merge_leaves_a_hook_entry_for_another_event_alone_under_this_event()
    {
        // "ariadne hook stop" under PreToolUse is a misplaced entry, not ours for that event: it is kept as is.
        var root = Parse(MergeOk(
            """{"hooks":{"PreToolUse":[{"matcher":"Write|Edit|MultiEdit","hooks":[{"type":"command","command":"ariadne hook stop"}]}]}}"""));

        Commands(Groups(root, "PreToolUse")).Should().Equal("ariadne hook stop", $"{Prefix} pre-tool-use");
    }

    [Fact]
    public void Merge_does_not_treat_ariadne_without_a_subcommand_as_ours()
    {
        var root = Parse(MergeOk(
            """{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"ariadne hook"},{"type":"command","command":"other ariadne hook stop"}]}]}}"""));

        Commands(Groups(root, "Stop")).Should().Equal("ariadne hook", "other ariadne hook stop", $"{Prefix} stop");
    }

    [Fact]
    public void Merge_skips_groups_without_a_hooks_array_and_adds_its_own_group()
    {
        var root = Parse(MergeOk("""{"hooks":{"Stop":["note",{"matcher":"x"},{"hooks":[{"type":"command","command":"ariadne hook stop"}]}]}}"""));

        var stop = Groups(root, "Stop");
        stop.Should().HaveCount(3);
        ((string)stop[0]!).Should().Be("note");
        ((string)stop[1]!["matcher"]!).Should().Be("x");
        ((string)((JsonArray)stop[2]!["hooks"]!)[0]!["command"]!).Should().Be($"{Prefix} stop");
    }

    [Fact]
    public void Merge_leaves_entries_without_a_string_command_alone()
    {
        var root = Parse(MergeOk(
            """{"hooks":{"Stop":[{"hooks":[{"type":"command"},{"type":"command","command":7},"text"]}]}}"""));

        var entries = (JsonArray)Groups(root, "Stop")[0]!["hooks"]!;
        entries.Should().HaveCount(4);
        entries[0]!.ToJsonString().Should().Be("""{"type":"command"}""");
        entries[1]!.ToJsonString().Should().Be("""{"type":"command","command":7}""");
        ((string)entries[3]!["command"]!).Should().Be($"{Prefix} stop");
    }
}
