using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Workspaces;

namespace Ariadne.NET.Hooks;

/// <summary>
/// Checks the solution when a turn ends, if the agent edited C# files, and feeds the violations back so the agent can fix
/// them. It never blocks: any failure is a warning on stderr and the turn ends as usual.
/// </summary>
public sealed class StopHook(IPackSetValidator validator, Func<string, string?>? getEnvironmentVariable = null) : IHook
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
            ForgetAll(input.SessionId, warn);
            return null;
        }

        IReadOnlyList<EditedRecord> edited;
        try
        {
            edited = EditedFiles.ReadRecords(input.SessionId);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            warn.WriteLine($"ariadne: could not read the edited files: {TextLimit.Shorten(e.Message)}");
            return null;
        }

        if (!edited.Any(r => r.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var root = ProjectRoot.Resolve(input.Cwd, getEnvironmentVariable);
        var solution = SolutionDiscovery.Find(root);
        if (solution is null)
        {
            // Without packs there is nothing to check, so only a project with packs hears about the missing solution.
            if (HasPackFile(root))
            {
                warn.WriteLine("ariadne: found no single solution to validate; skipping the check.");
            }

            return null;
        }

        var report = await ValidateAsync(validator, solution, warn, cancellationToken).ConfigureAwait(false);
        if (report is null)
        {
            return null;
        }

        Forget(edited, warn);
        return report.IsValid ? null : HookOutput.StopFeedback(ViolationFeedback.Build(report, root));
    }

    /// <summary>
    /// Validates against every pack under <c>.architecture</c>. Returns null, silently, when the project has no packs, and
    /// warns once and returns null when the validation failed. A validator that throws propagates to
    /// <see cref="HookHost"/>, which reports it as one stderr line; the edit list is kept because nothing forgets it then.
    /// </summary>
    private static async Task<ArchitectureReport?> ValidateAsync(
        IPackSetValidator validator, string solution, TextWriter warn, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(solution, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return result.Value;
        }

        if (result.Error.Code != AriadneErrorCode.NotFound)
        {
            warn.WriteLine($"ariadne: could not validate the solution: {TextLimit.Shorten(result.Error.Message.ReplaceLineEndings(" "))}");
        }

        return null;
    }

    private static bool HasPackFile(string root)
    {
        var folder = Path.Combine(root, ".architecture");
        try
        {
            return Directory.Exists(folder) && Directory.EnumerateFiles(folder).Any(PackDiscovery.IsPackFile);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    // Only what this run read: an edit recorded while the check ran stays for the next one.
    private static void Forget(IReadOnlyList<EditedRecord> seen, TextWriter warn)
    {
        try
        {
            EditedFiles.Remove(seen);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            warn.WriteLine($"ariadne: could not clear the edited files: {TextLimit.Shorten(e.Message)}");
        }
    }

    private static void ForgetAll(string sessionId, TextWriter warn)
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
