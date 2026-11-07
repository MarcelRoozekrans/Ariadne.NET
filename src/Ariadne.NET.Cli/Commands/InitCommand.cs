using System.CommandLine;
using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;
using Microsoft.Extensions.DependencyInjection;
using ZeroAlloc.Results;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Ariadne.NET.Cli.Commands;

public static class InitCommand
{
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
                $"Unknown agent '{agent}'. The only accepted value is: claude."));
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

        var installHooks = agent is not null;
        var rulesPath = Path.Combine(".architecture", "rules.yaml");
        var keepRules = installHooks && File.Exists(rulesPath);

        // Compute the merged settings before writing anything, so an unusable settings file aborts untouched.
        var settingsPath = Path.Combine(".claude", "settings.json");
        string? settingsText = null;
        if (installHooks)
        {
            var settings = MergeSettings(settingsPath);
            if (settings.IsFailure)
            {
                return ErrorOutput.Report(settings.Error);
            }

            settingsText = settings.Value;
        }

        if (keepRules)
        {
            Console.WriteLine("Kept the existing .architecture/rules.yaml");
        }
        else
        {
            WriteRules(rulesPath, template, extends.Value);
        }

        if (settingsText is not null)
        {
            WriteAtomically(settingsPath, settingsText);
            Console.WriteLine($"Added the Ariadne hooks to {ClaudeSettingsDisplay}");
        }

        return ExitCodes.Success;
    }

    private static Result<string, AriadneError> MergeSettings(string settingsPath)
    {
        var merged = AgentSettings.Merge(File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null);
        return merged.IsSuccess
            ? merged
            : Result<string, AriadneError>.Failure(new AriadneError(
                merged.Error.Code,
                $"Cannot update {ClaudeSettingsDisplay}. {merged.Error.Message}",
                "Fix or remove the file and run the command again; nothing was written."));
    }

    private static void WriteRules(string rulesPath, string template, IList<string> extends)
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

        Directory.CreateDirectory(".architecture");
        File.WriteAllText(rulesPath, serializer.Serialize(rulesContent));
        Console.WriteLine($"Initialized .architecture/rules.yaml with template: {template}");
    }

    private const string ClaudeSettingsDisplay = ".claude/settings.json";

    // Write beside the target and move over it, so a crash cannot leave a half-written file.
    private static void WriteAtomically(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, content, new System.Text.UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private static string PackName(string directory)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
        return string.IsNullOrWhiteSpace(name) ? "architecture" : name;
    }
}
