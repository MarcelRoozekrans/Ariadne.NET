using Ariadne.NET.Sync;

namespace Ariadne.NET.Hooks;

/// <summary>Gives the agent the resolved rules when a session starts. Every start source behaves the same.</summary>
public sealed class SessionStartHook(IProjectRules rules, IImportsLoader importsLoader) : IHook
{
    private const string NoRulesText =
        "Ariadne has no rules for this project yet. Run 'ariadne init' to create .architecture/rules.yaml.";

    /// <inheritdoc />
    public string Event => "session-start";

    /// <inheritdoc />
    public Task<string?> RunAsync(HookInput input, TextWriter warn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var root = input.Cwd ?? Directory.GetCurrentDirectory();
        string context;
        if (!File.Exists(Path.Combine(root, ".architecture", "rules.yaml")))
        {
            context = NoRulesText;
        }
        else
        {
            var loaded = rules.Load(root);
            if (loaded.IsFailure)
            {
                context = $"Ariadne could not load the project rules: {TextLimit.Shorten(loaded.Error.Message)}";
            }
            else
            {
                var cache = CacheFreshness.Read(root, importsLoader);
                context = SessionContext.Build(loaded.Value, cache.Versions, cache.Warnings);
            }
        }

        return Task.FromResult<string?>(HookOutput.SessionStart(context));
    }
}
