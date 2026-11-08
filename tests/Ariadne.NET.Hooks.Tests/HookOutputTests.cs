using System.Text.Json;
using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class HookOutputTests
{
    [Fact]
    public void SessionStart_is_the_documented_document()
    {
        HookOutput.SessionStart("hello").Should().Be(
            """{"hookSpecificOutput":{"hookEventName":"SessionStart","additionalContext":"hello"}}""");
    }

    [Fact]
    public void PreToolUseAsk_is_the_documented_document()
    {
        HookOutput.PreToolUseAsk("it's new").Should().Be(
            """{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"ask","permissionDecisionReason":"it's new"}}""");
    }

    [Fact]
    public void StopFeedback_is_the_documented_document()
    {
        HookOutput.StopFeedback("fix it").Should().Be(
            """{"hookSpecificOutput":{"hookEventName":"Stop","additionalContext":"fix it"}}""");
    }

    [Fact]
    public void Quotes_backslashes_and_newlines_are_escaped_and_round_trip()
    {
        const string text = "say \"hi\"\r\nC:\\dir\nline";

        foreach (var json in new[] { HookOutput.SessionStart(text), HookOutput.StopFeedback(text) })
        {
            json.Should().NotContain("\n");
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString().Should().Be(text);
        }

        using var ask = JsonDocument.Parse(HookOutput.PreToolUseAsk(text));
        ask.RootElement.GetProperty("hookSpecificOutput").GetProperty("permissionDecisionReason").GetString().Should().Be(text);
    }
}
