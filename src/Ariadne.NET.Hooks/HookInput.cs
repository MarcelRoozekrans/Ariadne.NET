using System.Text.Json;

namespace Ariadne.NET.Hooks;

/// <summary>The part of the JSON that Claude Code sends on stdin which the hooks use.</summary>
public sealed record HookInput
{
    /// <summary>The agent session id.</summary>
    public required string SessionId { get; init; }

    /// <summary>The agent's working directory.</summary>
    public string? Cwd { get; init; }

    /// <summary>The tool a <c>PreToolUse</c> input is about.</summary>
    public string? ToolName { get; init; }

    /// <summary><c>tool_input.file_path</c>, when present.</summary>
    public string? FilePath { get; init; }

    /// <summary><c>tool_input.content</c>, when present.</summary>
    public string? Content { get; init; }

    /// <summary>True when a <c>Stop</c> hook already continued this turn.</summary>
    public bool StopHookActive { get; init; }

    /// <summary>How a session started, on <c>SessionStart</c>.</summary>
    public string? Source { get; init; }

    /// <summary>
    /// Parses the stdin document. Returns null for empty or non-JSON input, for a document that is not an object and for
    /// one without a string <c>session_id</c>. Fields of an unexpected type read as absent.
    /// </summary>
    public static HookInput? TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var sessionId = GetString(root, "session_id");
            if (sessionId is null)
            {
                return null;
            }

            string? filePath = null;
            string? content = null;
            if (root.TryGetProperty("tool_input", out var toolInput) && toolInput.ValueKind == JsonValueKind.Object)
            {
                filePath = GetString(toolInput, "file_path");
                content = GetString(toolInput, "content");
            }

            return new HookInput
            {
                SessionId = sessionId,
                Cwd = GetString(root, "cwd"),
                ToolName = GetString(root, "tool_name"),
                FilePath = filePath,
                Content = content,
                StopHookActive = root.TryGetProperty("stop_hook_active", out var active) && active.ValueKind == JsonValueKind.True,
                Source = GetString(root, "source"),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
