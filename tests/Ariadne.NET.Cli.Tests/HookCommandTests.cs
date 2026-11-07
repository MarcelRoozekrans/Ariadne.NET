using System.Text.Json;
using Ariadne.NET.Cli;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Tests;

public sealed class HookCommandTests : IDisposable
{
    private readonly string _originalDirectory;
    private readonly TextReader _originalIn;
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly string _tempDir;
    private readonly ServiceProvider _provider;
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();

    public HookCommandTests()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _originalIn = Console.In;
        _originalOut = Console.Out;
        _originalError = Console.Error;
        _tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-hook-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, ".architecture"));
        File.WriteAllText(
            Path.Combine(_tempDir, ".architecture", "rules.yaml"),
            """
            schema: 2
            name: shop
            extends:
              - builtin://clean-architecture
            """);
        Directory.SetCurrentDirectory(_tempDir);

        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        services.AddAriadneNETWorkspaces();
        services.AddAriadneNETHooks();
        _provider = services.BuildServiceProvider();

        Console.SetOut(_stdout);
        Console.SetError(_stderr);
    }

    public void Dispose()
    {
        Console.SetIn(_originalIn);
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        Directory.SetCurrentDirectory(_originalDirectory);
        _provider.Dispose();
        _stdout.Dispose();
        _stderr.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    // Output is snapshotted before any assertion runs: FluentAssertions prints a licence banner to Console.Out on first use.
    private (int Exit, string Out, string Err) Run(string stdin, IServiceProvider? provider = null, params string[] args)
    {
        Console.SetIn(new StringReader(stdin));
        var exit = CliRootCommand.InvokeAsync(provider ?? _provider, args).GetAwaiter().GetResult();
        return (exit, _stdout.ToString(), _stderr.ToString());
    }

    [Fact]
    public void SessionStart_prints_a_SessionStart_document_with_additionalContext()
    {
        var input = JsonSerializer.Serialize(new { session_id = "s1", cwd = _tempDir, hook_event_name = "SessionStart" });

        var (exit, stdout, stderr) = Run(input, null, "hook", "session-start");

        exit.Should().Be(0);
        stderr.Should().BeEmpty();
        using var doc = JsonDocument.Parse(stdout);
        var specific = doc.RootElement.GetProperty("hookSpecificOutput");
        specific.GetProperty("hookEventName").GetString().Should().Be("SessionStart");
        specific.GetProperty("additionalContext").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Stop_with_empty_stdin_exits_zero_with_empty_stdout_and_a_stderr_line()
    {
        var (exit, stdout, stderr) = Run("", null, "hook", "stop");

        exit.Should().Be(0);
        stdout.Should().BeEmpty();
        stderr.Should().StartWith("ariadne hook: stop:");
    }

    [Fact]
    public void Unknown_event_exits_zero_with_a_stderr_line_and_empty_stdout()
    {
        var (exit, stdout, stderr) = Run("""{"session_id":"s1"}""", null, "hook", "nonsense");

        exit.Should().Be(0);
        stdout.Should().BeEmpty();
        stderr.Should().StartWith("ariadne hook: nonsense:");
    }

    [Fact]
    public void Missing_event_argument_is_a_usage_error()
    {
        var (exit, stdout, stderr) = Run("", null, "hook");

        exit.Should().NotBe(0);
        stderr.Should().NotBeEmpty();
    }

    [Fact]
    public void Unresolvable_HookHost_fails_open_with_one_stderr_line()
    {
        // No AddAriadneNETHooks: resolving HookHost throws InvalidOperationException.
        var services = new ServiceCollection();
        services.AddAriadneNETServices();
        using var bare = services.BuildServiceProvider();

        var (exit, stdout, stderr) = Run("""{"session_id":"s1"}""", bare, "hook", "session-start");

        exit.Should().Be(0);
        stdout.Should().BeEmpty();
        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().ContainSingle().Which.Should().StartWith("ariadne hook:");
    }
}
