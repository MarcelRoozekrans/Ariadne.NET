using System.Runtime.Versioning;
using System.Text;
using Ariadne.NET.Cli;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Ariadne.NET.Cli.Tests;

public sealed class InitAgentTests : IDisposable
{
    private readonly string _originalDirectory;
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly string _tempDir;
    private readonly ServiceProvider _provider;
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public InitAgentTests()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _originalOut = Console.Out;
        _originalError = Console.Error;
        _tempDir = Path.Combine(Path.GetTempPath(), $"ariadne-init-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(_tempDir);
            Directory.SetCurrentDirectory(_tempDir);
            var services = new ServiceCollection();
            services.AddAriadneNETServices();
            _provider = services.BuildServiceProvider();
            Console.SetOut(_out);
            Console.SetError(_err);
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
        _out.Dispose();
        _err.Dispose();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private void Restore()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        Directory.SetCurrentDirectory(_originalDirectory);
    }

    private static readonly string Rules = Path.Combine(".architecture", "rules.yaml");
    private static readonly string Settings = Path.Combine(".claude", "settings.json");

    private int Run(params string[] args) => CliRootCommand.InvokeAsync(_provider, args).GetAwaiter().GetResult();

    [Fact]
    public void Init_agent_claude_writes_rules_and_settings()
    {
        var exit = Run("init", "--agent", "claude");
        var output = _out.ToString();
        var rulesExists = File.Exists(Rules);
        var settings = File.Exists(Settings) ? File.ReadAllText(Settings) : "";

        exit.Should().Be(0);
        rulesExists.Should().BeTrue();
        settings.Should().Contain("ariadne hook stop");
        output.Should().Contain("Added the Ariadne hooks to .claude/settings.json");
    }

    [Fact]
    public void Init_agent_is_case_insensitive()
    {
        var exit = Run("init", "--agent", "Claude");
        var exists = File.Exists(Settings);

        exit.Should().Be(0);
        exists.Should().BeTrue();
    }

    [Fact]
    public void Init_agent_claude_keeps_an_existing_rules_file_byte_identical()
    {
        Directory.CreateDirectory(".architecture");
        var original = new UTF8Encoding(false).GetBytes("schema: 2\nname: mine\nextends:\n  - builtin://cqrs\n");
        File.WriteAllBytes(Rules, original);

        var exit = Run("init", "--agent", "claude");
        var output = _out.ToString();
        var after = File.ReadAllBytes(Rules);
        var settingsExists = File.Exists(Settings);

        exit.Should().Be(0);
        after.Should().Equal(original);
        settingsExists.Should().BeTrue();
        output.Should().Contain("Kept the existing .architecture/rules.yaml");
    }

    [Fact]
    public void Init_agent_claude_twice_leaves_settings_unchanged()
    {
        Run("init", "--agent", "claude");
        var first = File.ReadAllBytes(Settings);

        var exit = Run("init", "--agent", "claude");
        var second = File.ReadAllBytes(Settings);

        exit.Should().Be(0);
        second.Should().Equal(first);
    }

    [Fact]
    public void Init_agent_claude_keeps_foreign_settings()
    {
        Directory.CreateDirectory(".claude");
        File.WriteAllText(Settings, """{"permissions":{"allow":["Bash(ls)"]}}""");

        var exit = Run("init", "--agent", "claude");
        var settings = File.ReadAllText(Settings);

        exit.Should().Be(0);
        settings.Should().Contain("Bash(ls)").And.Contain("ariadne hook session-start");
    }

    [Fact]
    public void Init_with_another_agent_exits_2_and_writes_nothing()
    {
        var exit = Run("init", "--agent", "other");
        var error = _err.ToString();
        var rulesExists = File.Exists(Rules);
        var claudeExists = Directory.Exists(".claude");

        exit.Should().Be(2);
        error.Should().Contain("claude");
        rulesExists.Should().BeFalse();
        claudeExists.Should().BeFalse();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{ // c\n}")]
    [InlineData("""{"hooks":{"Stop":"x"}}""")]
    public void Init_with_an_invalid_settings_file_exits_2_and_writes_nothing(string existing)
    {
        Directory.CreateDirectory(".claude");
        File.WriteAllText(Settings, existing);

        var exit = Run("init", "--agent", "claude");
        var error = _err.ToString();
        var rulesExists = File.Exists(Rules);
        var after = File.ReadAllText(Settings);

        exit.Should().Be(2);
        error.Should().Contain(".claude");
        rulesExists.Should().BeFalse();
        after.Should().Be(existing);
        Directory.GetFiles(".claude").Should().HaveCount(1);
    }

    [Fact]
    public void Init_without_agent_still_overwrites_rules_and_writes_no_settings()
    {
        Directory.CreateDirectory(".architecture");
        File.WriteAllText(Rules, "old");

        var exit = Run("init");
        var rules = File.ReadAllText(Rules);
        var claudeExists = Directory.Exists(".claude");

        exit.Should().Be(0);
        rules.Should().Contain("schema: 2");
        claudeExists.Should().BeFalse();
    }

    private (int Exit, string Out, string Err) RunCapture(params string[] args)
    {
        var exit = Run(args);
        return (exit, _out.ToString(), _err.ToString());
    }

    private static string[] TempFiles() =>
        Directory.Exists(".claude") ? Directory.GetFiles(".claude", "*.tmp") : [];

    [Fact]
    public void Init_agent_claude_when_dot_claude_is_a_file_fails_cleanly()
    {
        File.WriteAllText(".claude", "x");

        var run = RunCapture("init", "--agent", "claude");
        var rulesExists = File.Exists(Rules);

        run.Exit.Should().Be(2);
        run.Err.Should().Contain("Cannot write .claude/settings.json").And.Contain("Nothing was written.");
        run.Out.Should().NotContain("Added");
        rulesExists.Should().BeFalse();
    }

    [Fact]
    public void Init_agent_claude_when_settings_is_a_directory_fails_cleanly()
    {
        Directory.CreateDirectory(Settings);

        var run = RunCapture("init", "--agent", "claude");
        var rulesExists = File.Exists(Rules);
        var temps = TempFiles();

        run.Exit.Should().Be(2);
        run.Err.Should().Contain("is a directory");
        run.Out.Should().NotContain("Added");
        rulesExists.Should().BeFalse();
        temps.Should().BeEmpty();
    }

    [Fact]
    public void Init_agent_claude_when_settings_is_locked_fails_cleanly()
    {
        Directory.CreateDirectory(".claude");
        File.WriteAllText(Settings, "{}");
        string err;
        string output;
        int exit;
        bool rulesExists;
        string[] temps;
        using (new FileStream(Settings, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            (exit, output, err) = RunCapture("init", "--agent", "claude");
            rulesExists = File.Exists(Rules);
            temps = TempFiles();
        }

        exit.Should().Be(2);
        err.Should().Contain("Cannot read .claude/settings.json");
        output.Should().NotContain("Added");
        rulesExists.Should().BeFalse();
        temps.Should().BeEmpty();
    }

    [Fact]
    public void Init_agent_claude_with_non_UTF8_settings_fails_and_leaves_the_file()
    {
        Directory.CreateDirectory(".claude");
        byte[] original = [0x7B, 0x22, 0x61, 0x22, 0x3A, 0x22, 0xFF, 0xFE, 0x22, 0x7D];
        File.WriteAllBytes(Settings, original);

        var run = RunCapture("init", "--agent", "claude");
        var after = File.ReadAllBytes(Settings);
        var rulesExists = File.Exists(Rules);

        run.Exit.Should().Be(2);
        run.Err.Should().Contain("not valid UTF-8");
        run.Out.Should().NotContain("Added");
        after.Should().Equal(original);
        rulesExists.Should().BeFalse();
    }

    [Fact]
    public void Init_agent_claude_with_a_BOM_prefixed_settings_file_merges_and_drops_the_BOM()
    {
        Directory.CreateDirectory(".claude");
        File.WriteAllBytes(Settings, [0xEF, 0xBB, 0xBF, .. "{\"a\":1}"u8.ToArray()]);

        var exit = Run("init", "--agent", "claude");
        var bytes = File.ReadAllBytes(Settings);

        exit.Should().Be(0);
        bytes[0].Should().Be((byte)'{');
        Encoding.UTF8.GetString(bytes).Should().Contain("\"a\": 1").And.Contain("ariadne hook stop");
    }

    [Fact]
    public void Init_agent_claude_leaves_no_temp_files_and_a_rerun_keeps_the_modification_time()
    {
        Run("init", "--agent", "claude");
        var old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Settings, old);
        var temps = TempFiles();

        var exit = Run("init", "--agent", "claude");
        var output = _out.ToString();
        var stamp = File.GetLastWriteTimeUtc(Settings);
        var tempsAfter = TempFiles();

        exit.Should().Be(0);
        temps.Should().BeEmpty();
        tempsAfter.Should().BeEmpty();
        stamp.Should().Be(old);
        output.Should().Contain("Added the Ariadne hooks to .claude/settings.json");
    }

    [UnixFact]
    [UnsupportedOSPlatform("windows")]
    public void Init_agent_claude_writes_through_a_symlinked_settings_file_and_keeps_its_mode()
    {
        Directory.CreateDirectory(".claude");
        Directory.CreateDirectory("shared");
        var real = Path.GetFullPath(Path.Combine("shared", "real.json"));
        File.WriteAllText(real, """{"keep":true}""");
        File.SetUnixFileMode(real, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.CreateSymbolicLink(Settings, real);

        var exit = Run("init", "--agent", "claude");
        var linkTarget = new FileInfo(Settings).LinkTarget;
        var content = File.ReadAllText(real);
        var mode = File.GetUnixFileMode(real);

        exit.Should().Be(0);
        linkTarget.Should().Be(real);
        content.Should().Contain("\"keep\": true").And.Contain("ariadne hook stop");
        mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public void Init_with_a_long_unknown_agent_truncates_the_echoed_value()
    {
        var run = RunCapture("init", "--agent", new string('x', 500));

        run.Exit.Should().Be(2);
        run.Err.Length.Should().BeLessThan(200);
        run.Err.Should().Contain("...");
    }

    [Theory]
    [InlineData(true, "rules.yaml was written, but the hooks were not added.")]
    [InlineData(false, "Nothing was written.")]
    public void Commit_failure_says_what_was_and_was_not_written(bool rulesWritten, string expected)
    {
        Directory.CreateDirectory(".claude");
        var plan = new ClaudeSettingsFile.Plan(Path.GetFullPath(Settings), Path.GetFullPath(Path.Combine(".claude", "missing.tmp")));

        var result = ClaudeSettingsFile.Commit(plan, rulesWritten);
        var failed = result.IsFailure;
        var message = failed ? result.Error.Message : "";
        var temps = TempFiles();
        var settingsExists = File.Exists(Settings);

        failed.Should().BeTrue();
        message.Should().Contain(expected).And.EndWith(".");
        temps.Should().BeEmpty();
        settingsExists.Should().BeFalse();
    }

    [UnixNonRootFact]
    [UnsupportedOSPlatform("windows")]
    public void Commit_failure_keeps_the_original_error_when_the_temp_file_cannot_be_deleted()
    {
        Directory.CreateDirectory(".claude");
        var temp = Path.GetFullPath(Path.Combine(".claude", "stuck.tmp"));
        File.WriteAllText(temp, "{}");
        File.SetUnixFileMode(".claude", UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var result = ClaudeSettingsFile.Commit(new ClaudeSettingsFile.Plan(Path.GetFullPath(Settings), temp), rulesWritten: true);
            var failed = result.IsFailure;
            var message = failed ? result.Error.Message : "";

            failed.Should().BeTrue();
            message.Should().Contain("rules.yaml was written, but the hooks were not added.");
        }
        finally
        {
            File.SetUnixFileMode(".claude", UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
