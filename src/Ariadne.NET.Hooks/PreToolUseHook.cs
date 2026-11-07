namespace Ariadne.NET.Hooks;

/// <summary>
/// Records the C# files the agent edits and asks the user before a new file lands outside every layer. It only ever asks,
/// it never denies.
/// </summary>
public sealed class PreToolUseHook(IProjectRules rules) : IHook
{
    private static bool IsEditTool(string tool) =>
        string.Equals(tool, "Write", StringComparison.Ordinal)
        || string.Equals(tool, "Edit", StringComparison.Ordinal)
        || string.Equals(tool, "MultiEdit", StringComparison.Ordinal);

    /// <inheritdoc />
    public string Event => "pre-tool-use";

    /// <inheritdoc />
    public Task<string?> RunAsync(HookInput input, TextWriter warn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.ToolName is null
            || !IsEditTool(input.ToolName)
            || input.FilePath is null
            || !input.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<string?>(null);
        }

        EditedFiles.Record(input.SessionId, input.FilePath);

        var root = input.Cwd ?? Directory.GetCurrentDirectory();
        var loaded = rules.Load(root);
        if (loaded.IsFailure)
        {
            return Task.FromResult<string?>(null);
        }

        var content = string.Equals(input.ToolName, "Write", StringComparison.Ordinal) ? input.Content : null;
        var question = NewFilePlacement.Check(loaded.Value, root, input.FilePath, content);
        return Task.FromResult(question is null ? null : HookOutput.PreToolUseAsk(question));
    }
}
