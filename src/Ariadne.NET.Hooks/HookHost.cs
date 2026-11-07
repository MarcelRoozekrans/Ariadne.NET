namespace Ariadne.NET.Hooks;

/// <summary>
/// Runs the hook for an event and fails open: whatever goes wrong becomes one line on stderr, empty stdout and exit code 0,
/// because a non-zero exit would block the agent's tool call.
/// </summary>
public sealed class HookHost(IEnumerable<IHook> hooks)
{
    private readonly IReadOnlyList<IHook> _hooks = [.. hooks];

    /// <summary>Reads the input from <paramref name="stdin"/>, runs the hook for <paramref name="eventName"/> and prints its output. Always returns 0.</summary>
    public async Task<int> RunAsync(
        string eventName, TextReader stdin, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stdin);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        try
        {
            var hook = _hooks.FirstOrDefault(h => string.Equals(h.Event, eventName, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("unknown event");
            cancellationToken.ThrowIfCancellationRequested();

            var text = await stdin.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var input = HookInput.TryParse(text)
                ?? throw new InvalidOperationException("stdin is not a hook input with a session_id");

            var output = await hook.RunAsync(input, stderr, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(output))
            {
                await stdout.WriteAsync(output).ConfigureAwait(false);
                await stdout.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // A hook must never fail the agent: every exception becomes a stderr line.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            try
            {
                await stderr.WriteLineAsync($"ariadne hook: {eventName}: {Describe(ex)}").ConfigureAwait(false);
            }
            catch (Exception writeError) when (writeError is IOException or ObjectDisposedException)
            {
                // stderr is gone; there is nowhere left to report to, and the exit code must stay 0.
            }
        }

        return 0;
    }

    private static string Describe(Exception ex) =>
        ex is OperationCanceledException ? "cancelled" : ex.Message.ReplaceLineEndings(" ");
}
