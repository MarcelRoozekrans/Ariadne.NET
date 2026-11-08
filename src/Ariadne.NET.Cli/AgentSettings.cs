using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ariadne.NET.Abstractions;
using ZeroAlloc.Results;

namespace Ariadne.NET.Cli;

/// <summary>
/// Merges the Ariadne hook entries into the text of a Claude Code <c>.claude/settings.json</c>, keeping everything else.
/// </summary>
internal static class AgentSettings
{
    /// <summary>The start of every command Ariadne installs; entries are recognised by it.</summary>
    internal const string HookCommandPrefix = "ariadne hook";

    private sealed record HookSpec(string Event, string? Matcher, string Argument, int TimeoutSeconds);

    // Claude Code's timeout is in seconds. The stop hook loads the solution, so it gets the most room.
    private static readonly HookSpec[] Specs =
    [
        new("SessionStart", null, "session-start", 30),
        new("PreToolUse", "Write|Edit|MultiEdit", "pre-tool-use", 10),
        new("Stop", null, "stop", 300),
    ];

    // Duplicate property names would silently lose one of the values on write, so they are a parse failure.
    private static readonly JsonDocumentOptions ParseOptions = new() { AllowDuplicateProperties = false };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Returns the new settings text, or <c>Invalid</c> when the existing text cannot be merged without losing data.</summary>
    internal static Result<string, AriadneError> Merge(string? existingJson)
    {
        var text = existingJson?.TrimStart('\uFEFF');
        JsonObject root;
        if (string.IsNullOrWhiteSpace(text))
        {
            root = new JsonObject();
        }
        else
        {
            JsonNode? parsed;
            try
            {
                parsed = JsonNode.Parse(text, nodeOptions: null, ParseOptions);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            {
                // Duplicate property names surface as ArgumentException from JsonNode.Parse, not JsonException.
                return Invalid($"The settings file is not valid JSON: {ex.Message.TrimEnd('.')}.");
            }

            if (parsed is not JsonObject obj)
            {
                return Invalid("The settings file must contain a JSON object.");
            }

            root = obj;
        }

        JsonObject hooks;
        if (root.TryGetPropertyValue("hooks", out var hooksNode))
        {
            if (hooksNode is not JsonObject existingHooks)
            {
                return Invalid("The \"hooks\" setting must be a JSON object.");
            }

            hooks = existingHooks;
        }
        else
        {
            hooks = new JsonObject();
            root["hooks"] = hooks;
        }

        foreach (var spec in Specs)
        {
            var failure = Apply(hooks, spec);
            if (failure is { } error)
            {
                return Result<string, AriadneError>.Failure(error);
            }
        }

        return Result<string, AriadneError>.Success(root.ToJsonString(WriteOptions) + "\n");
    }

    private static AriadneError? Apply(JsonObject hooks, HookSpec spec)
    {
        JsonArray groups;
        if (hooks.TryGetPropertyValue(spec.Event, out var groupsNode))
        {
            if (groupsNode is not JsonArray existing)
            {
                return InvalidError($"The \"hooks.{spec.Event}\" setting must be a JSON array.");
            }

            groups = existing;
        }
        else
        {
            groups = new JsonArray();
            hooks[spec.Event] = groups;
        }

        foreach (var node in groups)
        {
            if (node is JsonObject group && group.TryGetPropertyValue("hooks", out var entriesNode) && entriesNode is not JsonArray)
            {
                return InvalidError($"The \"hooks.{spec.Event}\" groups must each hold a \"hooks\" array.");
            }
        }

        var entry = new JsonObject
        {
            ["type"] = "command",
            ["command"] = $"{HookCommandPrefix} {spec.Argument}",
            ["timeout"] = spec.TimeoutSeconds,
        };

        var (placed, matchingEntries) = UpdateInPlace(groups, spec, entry);
        if (!placed)
        {
            AddEntry(groups, matchingEntries, spec, entry);
        }

        return null;
    }

    // Rewrites the first of our entries that sits in a group with the right matcher, and drops the others
    // (stale duplicates, or entries under another matcher), pruning groups that end up empty.
    private static (bool Placed, JsonArray? MatchingEntries) UpdateInPlace(JsonArray groups, HookSpec spec, JsonObject entry)
    {
        var placed = false;
        JsonArray? matchingEntries = null;
        for (var g = 0; g < groups.Count; g++)
        {
            if (groups[g] is not JsonObject group || group["hooks"] is not JsonArray entries)
            {
                continue;
            }

            var sameMatcher = string.Equals(MatcherOf(group), spec.Matcher, StringComparison.Ordinal);
            var removedAny = false;
            for (var e = 0; e < entries.Count; e++)
            {
                if (entries[e] is not JsonObject existing || !IsOurs(existing, spec))
                {
                    continue;
                }

                if (sameMatcher && !placed)
                {
                    existing["type"] = entry["type"]!.DeepClone();
                    existing["command"] = entry["command"]!.DeepClone();
                    existing["timeout"] = entry["timeout"]!.DeepClone();
                    placed = true;
                }
                else
                {
                    entries.RemoveAt(e);
                    e--;
                    removedAny = true;
                }
            }

            if (removedAny && entries.Count == 0)
            {
                groups.RemoveAt(g);
                g--;
            }
            else if (sameMatcher)
            {
                matchingEntries ??= entries;
            }
        }

        return (placed, matchingEntries);
    }

    private static void AddEntry(JsonArray groups, JsonArray? matchingEntries, HookSpec spec, JsonObject entry)
    {
        if (matchingEntries is not null)
        {
            matchingEntries.Add(entry);
            return;
        }

        var group = new JsonObject();
        if (spec.Matcher is not null)
        {
            group["matcher"] = spec.Matcher;
        }

        group["hooks"] = new JsonArray(entry);
        groups.Add(group);
    }

    private static string? MatcherOf(JsonObject group)
    {
        var matcher = group["matcher"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        return string.IsNullOrEmpty(matcher) ? null : matcher;
    }

    // An entry is ours for an event when its command runs "ariadne hook <that event's subcommand>", with or without a
    // path in front of ariadne and with or without trailing arguments. An "ariadne hook stop" entry sitting under
    // PreToolUse is a misplaced entry the user arranged themselves, so it is left alone, like any foreign entry.
    // "ariadne hooks-other" and "ariadne hook" without a subcommand never match.
    private static bool IsOurs(JsonObject entry, HookSpec spec)
    {
        if (entry["command"] is not JsonValue v || !v.TryGetValue<string>(out var command))
        {
            return false;
        }

        var words = command.Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 3 || !string.Equals(words[1], "hook", StringComparison.Ordinal))
        {
            return false;
        }

        // The subcommand is the third word; the split limit keeps any trailing arguments in it.
        var subcommand = words[2].Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)[0];
        return string.Equals(subcommand, spec.Argument, StringComparison.Ordinal) && IsAriadneExecutable(words[0]);
    }

    private static bool IsAriadneExecutable(string word)
    {
        var name = word[(word.LastIndexOfAny(['/', '\\']) + 1)..];
        return string.Equals(name, "ariadne", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "ariadne.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static Result<string, AriadneError> Invalid(string message) =>
        Result<string, AriadneError>.Failure(InvalidError(message));

    private static AriadneError InvalidError(string message) => new(AriadneErrorCode.Invalid, message);
}
