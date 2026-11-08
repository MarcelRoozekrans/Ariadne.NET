using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class HookInputTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("{\"cwd\":\"/work\"}")]
    [InlineData("{\"session_id\":42}")]
    [InlineData("{\"session_id\":null}")]
    public void TryParse_returns_null_for_unusable_input(string json)
    {
        HookInput.TryParse(json).Should().BeNull();
    }

    [Fact]
    public void TryParse_reads_a_session_start_input()
    {
        var input = HookInput.TryParse("""{"session_id":"s1","cwd":"/work","hook_event_name":"SessionStart","source":"startup"}""");

        input.Should().NotBeNull();
        input!.SessionId.Should().Be("s1");
        input.Cwd.Should().Be("/work");
        input.Source.Should().Be("startup");
        input.ToolName.Should().BeNull();
        input.StopHookActive.Should().BeFalse();
    }

    [Fact]
    public void TryParse_reads_a_write_input()
    {
        var input = HookInput.TryParse("""
            {"session_id":"s1","cwd":"/work","tool_name":"Write",
             "tool_input":{"file_path":"/work/src/A.cs","content":"class A {}"}}
            """);

        input!.ToolName.Should().Be("Write");
        input.FilePath.Should().Be("/work/src/A.cs");
        input.Content.Should().Be("class A {}");
    }

    [Fact]
    public void TryParse_gives_no_file_path_for_a_bash_input()
    {
        var input = HookInput.TryParse("""{"session_id":"s1","tool_name":"Bash","tool_input":{"command":"ls"}}""");

        input!.ToolName.Should().Be("Bash");
        input.FilePath.Should().BeNull();
        input.Content.Should().BeNull();
    }

    [Fact]
    public void TryParse_reads_stop_hook_active()
    {
        HookInput.TryParse("""{"session_id":"s1","stop_hook_active":true}""")!.StopHookActive.Should().BeTrue();
    }

    [Fact]
    public void TryParse_ignores_fields_of_the_wrong_type()
    {
        var input = HookInput.TryParse("""{"session_id":"s1","cwd":5,"tool_input":"x","stop_hook_active":"yes"}""");

        input!.Cwd.Should().BeNull();
        input.FilePath.Should().BeNull();
        input.StopHookActive.Should().BeFalse();
    }
}
