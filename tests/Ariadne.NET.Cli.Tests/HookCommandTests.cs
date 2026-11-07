using System.Text;
using System.Text.Json;
using Ariadne.NET.Cli;
using Ariadne.NET.Cli.Commands;
using Ariadne.NET.Hooks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Tests;

public sealed class HookCommandTests : IDisposable
{
    private static readonly UTF8Encoding Utf8 = new(false);

    private static readonly JsonSerializerOptions RawUtf8 = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly string _originalDirectory;
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly string _tempDir;
    private readonly ServiceProvider _provider;
    private readonly StringWriter _consoleOut = new();
    private readonly StringWriter _consoleError = new();

    public HookCommandTests()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _originalOut = Console.Out;
        _originalError = Console.Error;
        _tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-hook-{Guid.NewGuid():N}");
        try
        {
            WriteRules(_tempDir, "Core is a library every host uses.");
            Directory.SetCurrentDirectory(_tempDir);

            var services = new ServiceCollection();
            services.AddAriadneNETServices();
            services.AddAriadneNETWorkspaces();
            services.AddAriadneNETHooks();
            _provider = services.BuildServiceProvider();

            Console.SetOut(_consoleOut);
            Console.SetError(_consoleError);
        }
        catch
        {
            // xUnit does not call Dispose when the constructor throws.
            Restore();
            throw;
        }
    }

    public void Dispose()
    {
        Restore();
        _provider.Dispose();
        _consoleOut.Dispose();
        _consoleError.Dispose();
        DeleteQuietly(_tempDir);
    }

    private void Restore()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        Directory.SetCurrentDirectory(_originalDirectory);
    }

    private static void DeleteQuietly(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    private static void WriteRules(string dir, string because)
    {
        Directory.CreateDirectory(Path.Combine(dir, ".architecture"));
        File.WriteAllText(
            Path.Combine(dir, ".architecture", "rules.yaml"),
            $$"""
            schema: 2
            name: shop
            layers:
              - name: Core
                id: SHOP-CORE
                because: The core holds the logic.
                assemblies: ["Shop.Core"]
                allowedDependencies: []
            rules:
              - id: SHOP-NO-HOSTS
                because: {{because}}
                description: "Core never references a host"
                forbid:
                  from:
                    layers: [Core]
                  to:
                    assemblies: ["Shop.Host"]
            """,
            Utf8);
    }

    private sealed record Outcome(int Exit, byte[] Out, byte[] Err)
    {
        public string OutText => Utf8.GetString(Out);

        public string ErrText => Utf8.GetString(Err);
    }

    // Output is read as bytes before any assertion runs: FluentAssertions prints a licence banner to Console.Out on first use.
    private Outcome Run(byte[] stdin, IServiceProvider? provider = null, params string[] args) =>
        Run(new MemoryStream(stdin), provider, args);

    private Outcome Run(Stream stdin, IServiceProvider? provider = null, params string[] args)
    {
        var stdout = new MemoryStream();
        var stderr = new MemoryStream();
        var root = CliRootCommand.Create(
            provider ?? _provider, HookCommand.Create(provider ?? _provider, () => new HookStreams(stdin, stdout, stderr)));
        var exit = CliRootCommand.InvokeAsync(root, args, CancellationToken.None).GetAwaiter().GetResult();
        return new Outcome(exit, stdout.ToArray(), stderr.ToArray());
    }

    private Outcome Run(string stdin, IServiceProvider? provider = null, params string[] args) =>
        Run(Utf8.GetBytes(stdin), provider, args);

    [Fact]
    public void SessionStart_prints_a_SessionStart_document_with_additionalContext()
    {
        var input = JsonSerializer.Serialize(new { session_id = "s1", cwd = _tempDir, hook_event_name = "SessionStart" });

        var run = Run(input, null, "hook", "session-start");

        run.Exit.Should().Be(0);
        run.ErrText.Should().BeEmpty();
        using var doc = JsonDocument.Parse(run.Out);
        var specific = doc.RootElement.GetProperty("hookSpecificOutput");
        specific.GetProperty("hookEventName").GetString().Should().Be("SessionStart");
        specific.GetProperty("additionalContext").GetString().Should().Contain("SHOP-NO-HOSTS");
    }

    [Fact]
    public void Non_ascii_cwd_and_rule_text_round_trip_as_utf8_without_a_bom()
    {
        const string Because = "Core stays clean — café règle 日本";
        var dir = Path.Combine(Path.GetTempPath(), $"ariadne-héok-{Guid.NewGuid():N}");
        try
        {
            WriteRules(dir, Because);
            // Raw non-ASCII on stdin, the way Claude Code writes it, so a wrong stdin encoding mangles the cwd too.
            var input = JsonSerializer.Serialize(new { session_id = "s1", cwd = dir }, RawUtf8);

            var run = Run(Utf8.GetBytes(input), null, "hook", "session-start");

            run.Exit.Should().Be(0);
            run.ErrText.Should().BeEmpty();
            run.Out.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, "the protocol is UTF-8 without a byte order mark");
            run.OutText.Should().NotContain("�");
            using var doc = JsonDocument.Parse(run.Out);
            doc.RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString()
                .Should().Contain(Because);
        }
        finally
        {
            DeleteQuietly(dir);
        }
    }

    [Fact]
    public void Stop_with_empty_stdin_exits_zero_with_empty_stdout_and_a_stderr_line()
    {
        var run = Run("", null, "hook", "stop");

        run.Exit.Should().Be(0);
        run.Out.Should().BeEmpty();
        run.ErrText.Should().StartWith("ariadne hook: stop:");
    }

    [Fact]
    public void Unknown_event_exits_zero_with_a_stderr_line_and_empty_stdout()
    {
        var run = Run("""{"session_id":"s1"}""", null, "hook", "nonsense");

        run.Exit.Should().Be(0);
        run.Out.Should().BeEmpty();
        run.ErrText.Should().StartWith("ariadne hook: nonsense:");
    }

    [Fact]
    public void Missing_event_argument_is_a_usage_error_that_runs_no_hook()
    {
        var input = JsonSerializer.Serialize(new { session_id = "s1", cwd = _tempDir });

        var run = Run(input, null, "hook");
        var console = _consoleOut.ToString() + _consoleError;

        run.Exit.Should().Be(1);
        run.Out.Should().BeEmpty();
        run.Err.Should().BeEmpty();
        console.Should().Contain("event").And.NotContain("hookSpecificOutput");
    }

    [Fact]
    public void Unresolvable_HookHost_fails_open_with_one_stderr_line()
    {
        // No AddAriadneNETHooks: resolving HookHost throws InvalidOperationException.
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        using var bare = services.BuildServiceProvider();

        var run = Run("""{"session_id":"s1"}""", bare, "hook", "session-start");
        var console = _consoleError.ToString();

        run.Exit.Should().Be(0);
        run.Out.Should().BeEmpty();
        console.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle().Which.Should().StartWith("ariadne hook:");
    }

    [Fact]
    public void A_stdin_that_throws_fails_open_with_one_stderr_line()
    {
        var run = Run(new ThrowingStream(), null, "hook", "session-start");

        run.Exit.Should().Be(0);
        run.Out.Should().BeEmpty();
        var lines = run.ErrText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().ContainSingle().Which.Should().StartWith("ariadne hook: session-start:").And.Contain("stdin closed");
    }

    [Fact]
    public void A_hook_whose_constructor_throws_fails_open_with_one_stderr_line()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHook>(_ => throw new InvalidOperationException("hook construction failed"));
        services.AddSingleton<HookHost>();
        using var broken = services.BuildServiceProvider();

        var run = Run("""{"session_id":"s1"}""", broken, "hook", "stop");
        var console = _consoleError.ToString();

        run.Exit.Should().Be(0);
        run.Out.Should().BeEmpty();
        run.Err.Should().BeEmpty();
        console.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle().Which.Should().StartWith("ariadne hook:");
    }

    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("stdin closed");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("stdin closed");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
