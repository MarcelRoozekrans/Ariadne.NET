using ZeroAlloc.Resilience;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

/// <summary>Runs one git command and returns its standard output.</summary>
[Retry(MaxAttempts = 3, BackoffMs = 500)]
[Timeout(Ms = 60_000)]
internal interface IGitClient
{
    /// <summary>
    /// Runs git with <paramref name="arguments"/> as its argv, one element per argument, so a value such as a url
    /// stays one argument whatever spaces or quotes it holds.
    /// </summary>
    ValueTask<Result<string, ResilienceError>> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct);
}
