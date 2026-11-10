using FluentAssertions;

namespace Ariadne.NET.Hooks.Tests;

public class HookHostTests
{
    private const string ValidInput = """{"session_id":"s1","cwd":"/work"}""";

    private sealed class FakeHook(string eventName, Func<HookInput, string?> run) : IHook
    {
        public string Event { get; } = eventName;

        public HookInput? Received { get; private set; }

        public Task<string?> RunAsync(HookInput input, TextWriter warn, CancellationToken cancellationToken)
        {
            Received = input;
            return Task.FromResult(run(input));
        }
    }

    private static async Task<(int Exit, string Out, string Err)> RunAsync(
        IEnumerable<IHook> hooks, string eventName, string stdin, CancellationToken ct = default)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exit = await new HookHost(hooks).RunAsync(eventName, new StringReader(stdin), stdout, stderr, ct).ConfigureAwait(false);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public async Task Writes_the_hook_output_to_stdout()
    {
        var hook = new FakeHook("session-start", _ => "{\"x\":1}");

        var (exit, output, err) = await RunAsync([hook], "session-start", ValidInput);

        exit.Should().Be(0);
        output.Should().Be("{\"x\":1}");
        err.Should().BeEmpty();
        hook.Received!.SessionId.Should().Be("s1");
    }

    [Fact]
    public async Task Writes_nothing_when_the_hook_is_silent()
    {
        var (exit, output, err) = await RunAsync([new FakeHook("stop", _ => null)], "stop", ValidInput);

        exit.Should().Be(0);
        output.Should().BeEmpty();
        err.Should().BeEmpty();
    }

    [Fact]
    public async Task Picks_the_hook_for_the_event()
    {
        var other = new FakeHook("stop", _ => "wrong");
        var wanted = new FakeHook("pre-tool-use", _ => "right");

        var (_, output, _) = await RunAsync([other, wanted], "pre-tool-use", ValidInput);

        output.Should().Be("right");
    }

    [Fact]
    public async Task A_throwing_hook_fails_open_with_one_stderr_line()
    {
        var hook = new FakeHook("stop", _ => throw new InvalidOperationException("boom"));

        var (exit, output, err) = await RunAsync([hook], "stop", ValidInput);

        exit.Should().Be(0);
        output.Should().BeEmpty();
        err.Should().StartWith("ariadne hook: ").And.Contain("stop").And.Contain("boom");
        err.TrimEnd().Split('\n').Should().HaveCount(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"cwd\":\"/work\"}")]
    public async Task Unusable_stdin_fails_open_with_one_stderr_line(string stdin)
    {
        var hook = new FakeHook("stop", _ => "must not run");

        var (exit, output, err) = await RunAsync([hook], "stop", stdin);

        exit.Should().Be(0);
        output.Should().BeEmpty();
        err.Should().StartWith("ariadne hook: ");
        err.TrimEnd().Split('\n').Should().HaveCount(1);
        hook.Received.Should().BeNull();
    }

    [Fact]
    public async Task An_unknown_event_fails_open_with_one_stderr_line()
    {
        var (exit, output, err) = await RunAsync([new FakeHook("stop", _ => "x")], "bogus", ValidInput);

        exit.Should().Be(0);
        output.Should().BeEmpty();
        err.Should().StartWith("ariadne hook: ").And.Contain("bogus");
        err.TrimEnd().Split('\n').Should().HaveCount(1);
    }

    [Fact]
    public async Task A_cancelled_token_fails_open_without_output()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var hook = new FakeHook("stop", _ => "x");

        var (exit, output, err) = await RunAsync([hook], "stop", ValidInput, cts.Token);

        exit.Should().Be(0);
        output.Should().BeEmpty();
        err.Should().StartWith("ariadne hook: ").And.Contain("cancelled");
        hook.Received.Should().BeNull();
    }

    [Fact]
    public async Task A_hook_that_ignores_cancellation_still_exits_0_once_cancelled()
    {
        // SIGTERM cancels the token, but a console read or a solution load does not observe it; the host must not wait for them.
        using var cts = new CancellationTokenSource();
        var never = new TaskCompletionSource<string?>();
        var hook = new BlockingHook("stop", never.Task, cts.Cancel);

        var run = RunAsync([hook], "stop", ValidInput, cts.Token);
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10)));

        finished.Should().BeSameAs(run);
        var (exit, output, err) = await run;
        exit.Should().Be(0);
        output.Should().BeEmpty();
        err.Should().StartWith("ariadne hook: stop: cancelled");
    }

    private sealed class BlockingHook(string eventName, Task<string?> result, Action onStart) : IHook
    {
        public string Event { get; } = eventName;

        public Task<string?> RunAsync(HookInput input, TextWriter warn, CancellationToken cancellationToken)
        {
            onStart();
            return result;
        }
    }

    [Fact]
    public async Task A_hook_that_throws_cancellation_fails_open_as_cancelled()
    {
        var hook = new FakeHook("stop", _ => throw new OperationCanceledException());

        var (exit, output, err) = await RunAsync([hook], "stop", ValidInput);

        exit.Should().Be(0);
        output.Should().BeEmpty();
        err.Should().StartWith("ariadne hook: stop: cancelled");
    }

    [Fact]
    public async Task A_multi_line_exception_message_collapses_to_one_stderr_line()
    {
        var hook = new FakeHook("stop", _ => throw new InvalidOperationException("first\r\nsecond\nthird"));

        var (_, _, err) = await RunAsync([hook], "stop", ValidInput);

        err.TrimEnd().Split('\n').Should().HaveCount(1);
        err.Should().Contain("first").And.Contain("second").And.Contain("third");
    }

    [Fact]
    public async Task A_huge_exception_message_gives_one_bounded_stderr_line()
    {
        var hook = new FakeHook("stop", _ => throw new InvalidOperationException(new string('x', 30_000) + "\nsecond"));

        var (exit, output, err) = await RunAsync([hook], "stop", ValidInput);

        exit.Should().Be(0);
        output.Should().BeEmpty();
        var lines = err.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().ContainSingle();
        lines[0].Length.Should().BeLessThan(700);
    }

    private sealed class BrokenWriter(Exception error) : TextWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        public override Task WriteLineAsync(string? value) => Task.FromException(error);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(ObjectDisposedException))]
    public async Task A_stderr_that_cannot_be_written_still_exits_zero(Type errorType)
    {
        var error = errorType == typeof(IOException) ? new IOException("pipe closed") : (Exception)new ObjectDisposedException("stderr");
        using var stdout = new StringWriter();
        using var stderr = new BrokenWriter(error);
        var hook = new FakeHook("stop", _ => throw new InvalidOperationException("boom"));

        var exit = await new HookHost([hook]).RunAsync("stop", new StringReader(ValidInput), stdout, stderr, CancellationToken.None);

        exit.Should().Be(0);
        stdout.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Null_streams_are_refused()
    {
        var host = new HookHost([]);

        Func<Task> noIn = () => host.RunAsync("stop", null!, TextWriter.Null, TextWriter.Null, CancellationToken.None);
        Func<Task> noOut = () => host.RunAsync("stop", TextReader.Null, null!, TextWriter.Null, CancellationToken.None);
        Func<Task> noErr = () => host.RunAsync("stop", TextReader.Null, TextWriter.Null, null!, CancellationToken.None);

        await noIn.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true);
        await noOut.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true);
        await noErr.Should().ThrowAsync<ArgumentNullException>().ConfigureAwait(true);
    }
}
