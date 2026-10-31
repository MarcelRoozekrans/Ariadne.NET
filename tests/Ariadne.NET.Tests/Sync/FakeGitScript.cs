using ZeroAlloc.Resilience;
using ZeroAlloc.Results;

namespace Ariadne.NET.Tests.Sync;

/// <summary>
/// Scripted git: <c>ls-remote</c> runs <see cref="LsRemote"/>; <c>clone</c> writes a pack file into the target,
/// the last argument, unless <see cref="CloneFails"/>. Every argv is recorded in <see cref="Invocations"/>.
/// </summary>
internal sealed class FakeGitScript
{
    public const string Tags = "abc123\trefs/tags/v1.0.0\n";

    /// <summary>Called with the 1-based attempt number; throws to fail the attempt, or returns the output.</summary>
    public Func<int, CancellationToken, string> LsRemote { get; set; } = (_, _) => Tags;

    public bool CloneFails { get; set; }

    /// <summary>Runs after a successful clone with the clone target, to add files such as links.</summary>
    public Action<string>? OnClone { get; set; }

    public List<IReadOnlyList<string>> Invocations { get; } = [];

    public int LsRemoteCalls { get; private set; }

    public int CloneCalls { get; private set; }

    public ValueTask<Result<string, ResilienceError>> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct)
    {
        Invocations.Add([.. arguments]);
        if (string.Equals(arguments[0], "ls-remote", StringComparison.Ordinal))
        {
            LsRemoteCalls++;
            return ValueTask.FromResult(Result<string, ResilienceError>.Success(LsRemote(LsRemoteCalls, ct)));
        }

        CloneCalls++;
        if (CloneFails)
        {
            return ValueTask.FromResult(Result<string, ResilienceError>.Failure(
                new ResilienceError("Retry", "fatal: clone failed", null)));
        }

        var target = arguments[^1];
        Directory.CreateDirectory(Path.Combine(target, "packs"));
        File.WriteAllText(Path.Combine(target, "pack.yaml"), "name: pack\n");
        File.WriteAllText(Path.Combine(target, "packs", "core.yaml"), "name: core\n");
        OnClone?.Invoke(target);
        return ValueTask.FromResult(Result<string, ResilienceError>.Success(string.Empty));
    }
}
