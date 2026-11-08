namespace Ariadne.NET.Hooks;

/// <summary>One agent hook: reads the event's input and returns the JSON to print, or null to stay silent.</summary>
public interface IHook
{
    /// <summary>The event this hook serves: <c>session-start</c>, <c>pre-tool-use</c> or <c>stop</c>.</summary>
    string Event { get; }

    /// <summary>Runs the hook. <paramref name="warn"/> is stderr, for a non-fatal problem the hook handles itself.</summary>
    Task<string?> RunAsync(HookInput input, TextWriter warn, CancellationToken cancellationToken);
}
