using System.CommandLine;
using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleLoader;
using Microsoft.Extensions.DependencyInjection;
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

        var command = new Command("init", "Initialize architecture rules for this project")
        {
            templateOption,
        };

        command.SetAction(parseResult =>
        {
            var template = parseResult.GetValue(templateOption)!;
            var registry = provider.GetRequiredService<IBuiltinPackRegistry>();

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

            var extendsList = extends.Value;

            // Schema 2 requires a name; the directory the pack belongs to is the natural one.
            var rulesContent = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["schema"] = 2,
                ["name"] = PackName(Directory.GetCurrentDirectory()),
                ["extends"] = extendsList,
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            Directory.CreateDirectory(".architecture");
            File.WriteAllText(
                Path.Combine(".architecture", "rules.yaml"),
                serializer.Serialize(rulesContent));

            Console.WriteLine($"Initialized .architecture/rules.yaml with template: {template}");
            return ExitCodes.Success;
        });

        return command;
    }

    private static string PackName(string directory)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
        return string.IsNullOrWhiteSpace(name) ? "architecture" : name;
    }
}
