using Ariadne.NET.Abstractions;
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
        var loaded = rules.Load(root);
        string context;
        if (loaded.IsFailure)
        {
            context = loaded.Error.Code == AriadneErrorCode.NotFound
                ? NoRulesText
                : $"Ariadne could not load the project rules: {loaded.Error.Message}";
        }
        else
        {
            var warnings = CacheFreshness.Check(root, importsLoader);
            var versions = CacheFreshness.Versions(root, importsLoader);
            context = SessionContext.Build(loaded.Value, versions, warnings);
        }

        return Task.FromResult<string?>(HookOutput.SessionStart(context));
    }
}
