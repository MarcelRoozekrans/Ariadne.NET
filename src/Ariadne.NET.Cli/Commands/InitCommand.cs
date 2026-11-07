using System.CommandLine;
using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ariadne.NET.Cli.Commands;

public static class InitCommand
{
    private const int MaxEchoedLength = 40;

    private static readonly string RulesPath = Path.Combine(".architecture", "rules.yaml");

    public static Command Create(IServiceProvider provider)
    {
        var templateOption = new Option<string>("--template", "-t")
        {
            Description = "Template to initialize (supports + for composition, e.g. clean-architecture+cqrs)",
            DefaultValueFactory = _ => "clean-architecture",
        };

        var agentOption = new Option<string?>("--agent")
        {
            Description = "Also install the hooks for a coding agent (accepted value: claude)",
        };

        var command = new Command("init", "Initialize architecture rules for this project")
        {
            templateOption,
            agentOption,
        };

        command.SetAction(parseResult => Execute(
            provider.GetRequiredService<IBuiltinPackRegistry>(),
            parseResult.GetValue(templateOption)!,
            parseResult.GetValue(agentOption)));

        return command;
    }

    private static int Execute(IBuiltinPackRegistry registry, string template, string? agent)
    {
        if (agent is not null && !string.Equals(agent, "claude", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorOutput.Report(new AriadneError(
                AriadneErrorCode.Invalid,
                $"Unknown agent '{Truncate(agent)}'. The only accepted value is: claude."));
        }

        var extends = TemplateReference.ToExtends(template);
        if (extends.IsFailure)
        {
            return ErrorOutput.Report(extends.Error);
        }

        // ToExtends succeeded, so the template has at least one name and ParseNames cannot fail.
        foreach (var name in TemplateReference.ParseNames(template).Value)
        {
            var pack = registry.GetPack(name);
            if (pack.IsFailure)
            {
                return ErrorOutput.Report(pack.Error);
            }
        }

        return agent is null
            ? WriteRules(template, extends.Value)
            : InitWithHooks(template, extends.Value);
    }

    // Everything that can fail on the settings file runs before rules.yaml is written; only the final move comes after.
    private static int InitWithHooks(string template, IList<string> extends)
    {
        var plan = ClaudeSettingsFile.Prepare(Path.Combine(".claude", "settings.json"));
        if (plan.IsFailure)
        {
            return ErrorOutput.Report(plan.Error);
        }

        var keepRules = File.Exists(RulesPath);
        if (keepRules)
        {
            Console.WriteLine("Kept the existing .architecture/rules.yaml");
        }
        else
        {
            var written = WriteRules(template, extends);
            if (written != ExitCodes.Success)
            {
                ClaudeSettingsFile.Discard(plan.Value);
                return written;
            }
        }

        var committed = ClaudeSettingsFile.Commit(plan.Value, rulesWritten: !keepRules);
        if (committed.IsFailure)
        {
            return ErrorOutput.Report(committed.Error);
        }

        Console.WriteLine($"Added the Ariadne hooks to {ClaudeSettingsFile.DisplayPath}");
        return ExitCodes.Success;
    }

    private static int WriteRules(string template, IList<string> extends)
    {
        // Schema 2 requires a name; the directory the pack belongs to is the natural one.
        var rulesContent = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["schema"] = 2,
            ["name"] = PackName(Directory.GetCurrentDirectory()),
            ["extends"] = extends,
        };

        // The name comes from the directory, so it may read as another YAML type, such as null, ~, true or 2.
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithQuotingNecessaryStrings()
            .Build();

        try
        {
            Directory.CreateDirectory(".architecture");
            File.WriteAllText(RulesPath, serializer.Serialize(rulesContent));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ErrorOutput.Report(new AriadneError(
                AriadneErrorCode.Unavailable, "Cannot write .architecture/rules.yaml.", ex.Message));
        }

        Console.WriteLine($"Initialized .architecture/rules.yaml with template: {template}");
        return ExitCodes.Success;
    }

    private static string Truncate(string value) => value.Length <= MaxEchoedLength ? value : value[..MaxEchoedLength] + "...";

    private static string PackName(string directory)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
        return string.IsNullOrWhiteSpace(name) ? "architecture" : name;
    }
}
