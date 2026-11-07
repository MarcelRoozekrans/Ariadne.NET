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
}
