using Ariadne.NET.Sync;
using ZeroAlloc.Resilience;
using ZeroAlloc.Results;

namespace Ariadne.NET.Tests.Sync;

/// <summary>
/// Stands in for <see cref="GitCliClient"/> behind the DI-built resilience proxy. It is transient, as
/// the generated registration makes it, so the script and the call counts live in <see cref="FakeGitScript"/>.
/// </summary>
internal sealed class FakeGitClient(FakeGitScript script) : IGitClient
{
    public ValueTask<Result<string, ResilienceError>> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct) =>
        script.RunAsync(arguments, ct);
}
