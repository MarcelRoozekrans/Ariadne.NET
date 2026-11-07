using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Ariadne.NET.Hooks;

/// <summary>Builds the JSON documents a hook writes to stdout for Claude Code.</summary>
public static class HookOutput
{
    /// <summary>Context that Claude Code adds at the start of a session.</summary>
    public static string SessionStart(string context) =>
        Build("SessionStart", ("additionalContext", context));

    /// <summary>Makes Claude Code ask the user before the tool call goes ahead.</summary>
    public static string PreToolUseAsk(string reason) =>
        Build("PreToolUse", ("permissionDecision", "ask"), ("permissionDecisionReason", reason));

    /// <summary>Feedback that lets the agent continue its turn.</summary>
    public static string StopFeedback(string text) =>
        Build("Stop", ("additionalContext", text));

    private static string Build(string eventName, params (string Name, string Value)[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("hookSpecificOutput");
            writer.WriteString("hookEventName", eventName);
            foreach (var (name, value) in fields)
            {
                writer.WriteString(name, value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
