using Ariadne.NET.Abstractions;
using Ariadne.NET.Workspaces;

namespace Ariadne.NET.Hooks;

/// <summary>
/// Checks the solution when a turn ends, if the agent edited C# files, and feeds the violations back so the agent can fix
/// them. It never blocks: any failure is a warning on stderr and the turn ends as usual.
/// </summary>
public sealed class StopHook(IProjectRules rules, ISolutionValidator validator) : IHook
{
    /// <inheritdoc />
    public string Event => "stop";

    /// <inheritdoc />
    public async Task<string?> RunAsync(HookInput input, TextWriter warn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(warn);

        if (input.StopHookActive)
        {
            // The agent already got feedback this turn; do not loop.
            Forget(input.SessionId, warn);
            return null;
        }

        IReadOnlyList<string> edited;
        try
        {
            edited = EditedFiles.Read(input.SessionId);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            warn.WriteLine($"ariadne: could not read the edited files: {TextLimit.Shorten(e.Message)}");
            return null;
        }

        if (!edited.Any(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var root = input.Cwd ?? Directory.GetCurrentDirectory();
        var loaded = rules.Load(root);
        if (loaded.IsFailure)
        {
            if (loaded.Error.Code != AriadneErrorCode.NotFound)
            {
                warn.WriteLine($"ariadne: could not load the project rules: {TextLimit.Shorten(loaded.Error.Message)}");
            }

            return null;
        }

        var solution = SolutionDiscovery.Find(root);
        if (solution is null)
        {
            warn.WriteLine("ariadne: found no single solution to validate; skipping the check.");
            return null;
        }

        var report = await validator.ValidateAsync(loaded.Value, solution, cancellationToken).ConfigureAwait(false);
        if (report.IsFailure)
        {
            warn.WriteLine($"ariadne: could not validate the solution: {TextLimit.Shorten(report.Error.Message)}");
            return null;
        }

        Forget(input.SessionId, warn);
        return report.Value.IsValid ? null : HookOutput.StopFeedback(ViolationFeedback.Build(report.Value, root));
    }

    private static void Forget(string sessionId, TextWriter warn)
    {
        try
        {
            EditedFiles.Clear(sessionId);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            warn.WriteLine($"ariadne: could not clear the edited files: {TextLimit.Shorten(e.Message)}");
        }
    }
}
