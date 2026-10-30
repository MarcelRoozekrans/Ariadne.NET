using System.CommandLine;
using ArchGov.Core.RuleLoader;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchGov.Host.Cli.Commands;

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

            // Validate all templates exist
            foreach (var name in TemplateReference.ParseNames(template))
            {
                registry.GetPack(name); // throws if not found
            }

            var extendsList = TemplateReference.ToExtends(template);

            var rulesContent = new Dictionary<string, object>(StringComparer.Ordinal)
            {
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
        });

        return command;
    }
}
