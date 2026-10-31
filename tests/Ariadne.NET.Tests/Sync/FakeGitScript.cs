using ZeroAlloc.Resilience;
using ZeroAlloc.Results;

namespace Ariadne.NET.Tests.Sync;

/// <summary>Scripted git: <c>ls-remote</c> runs <see cref="LsRemote"/>, <c>clone</c> writes one pack file.</summary>
internal sealed class FakeGitScript
{
    public const string Tags = "abc123\trefs/tags/v1.0.0\n";

    /// <summary>Called with the 1-based attempt number; throws to fail the attempt, or returns the output.</summary>
    public Func<int, CancellationToken, string> LsRemote { get; set; } = (_, _) => Tags;

    public int LsRemoteCalls { get; private set; }

    public int CloneCalls { get; private set; }

    public ValueTask<Result<string, ResilienceError>> RunAsync(string arguments, CancellationToken ct)
    {
        if (arguments.StartsWith("ls-remote", StringComparison.Ordinal))
        {
            LsRemoteCalls++;
            return ValueTask.FromResult(Result<string, ResilienceError>.Success(LsRemote(LsRemoteCalls, ct)));
        }

        CloneCalls++;
        var target = arguments[(arguments.IndexOf('"', StringComparison.Ordinal) + 1)..^1];
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "pack.yaml"), "name: pack\n");
        return ValueTask.FromResult(Result<string, ResilienceError>.Success(string.Empty));
    }
}
