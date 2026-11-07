namespace Ariadne.NET.Hooks;

/// <summary>
/// Records the C# files the agent edits and asks the user before a new file lands outside every layer. It only ever asks,
/// it never denies.
/// </summary>
public sealed class PreToolUseHook(IProjectRules rules, Func<string, string?>? getEnvironmentVariable = null) : IHook
{
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

        ArgumentNullException.ThrowIfNull(warn);

        // A relative path is relative to the agent's working directory, which is not always the project root.
        var filePath = input.Cwd is null ? input.FilePath : Path.GetFullPath(input.FilePath, input.Cwd);

        try
        {
            EditedFiles.Record(input.SessionId, filePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Losing the record must not lose the question.
            warn.WriteLine($"ariadne: could not record the edited file: {TextLimit.Shorten(e.Message)}");
        }

        var root = ProjectRoot.Resolve(input.Cwd, getEnvironmentVariable);
        if (!File.Exists(Path.Combine(root, ".architecture", "rules.yaml")))
        {
            return Task.FromResult<string?>(null);
        }

        var loaded = rules.Load(root);
        if (loaded.IsFailure)
        {
            warn.WriteLine($"ariadne: could not load the project rules: {TextLimit.Shorten(loaded.Error.Message)}");
            return Task.FromResult<string?>(null);
        }

        var content = string.Equals(input.ToolName, "Write", StringComparison.Ordinal) ? input.Content : null;
        var question = NewFilePlacement.Check(loaded.Value, root, filePath, content);
        return Task.FromResult(question is null ? null : HookOutput.PreToolUseAsk(question));
    }

    private static bool IsEditTool(string tool) =>
        string.Equals(tool, "Write", StringComparison.Ordinal)
        || string.Equals(tool, "Edit", StringComparison.Ordinal)
        || string.Equals(tool, "MultiEdit", StringComparison.Ordinal);
}
