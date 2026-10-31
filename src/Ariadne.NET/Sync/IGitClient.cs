using ZeroAlloc.Resilience;
using ZeroAlloc.Results;

namespace Ariadne.NET.Sync;

/// <summary>Runs one git command and returns its standard output.</summary>
[Retry(MaxAttempts = 3, BackoffMs = 500)]
[Timeout(Ms = 60_000)]
internal interface IGitClient
{
    ValueTask<Result<string, ResilienceError>> RunAsync(string arguments, CancellationToken ct);
}
