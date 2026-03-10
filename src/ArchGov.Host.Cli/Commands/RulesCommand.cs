using System.CommandLine;
using ArchGov.Core.RuleLoader;
using ArchGov.Core.RuleSchema;
using Microsoft.Extensions.DependencyInjection;

namespace ArchGov.Host.Cli.Commands;

public static class RulesCommand
{
    public static Command Create(IServiceProvider provider)
    {
        var command = new Command("rules", "Print resolved architecture rules");

        command.SetAction(_ =>
        {
            var pack = ResolveRulesFromFile(provider);

            Console.WriteLine("Resolved Architecture Rules");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine();

            Console.WriteLine("Layers:");
            Console.WriteLine($"  {"Name",-25} {"Allowed Dependencies"}");
            Console.WriteLine($"  {new string('-', 25)} {new string('-', 30)}");

            foreach (var layer in pack.Layers)
            {
                var deps = layer.AllowedDependencies.Count > 0
                    ? string.Join(", ", layer.AllowedDependencies)
                    : "(none)";
                Console.WriteLine($"  {layer.Name,-25} {deps}");
            }

            Console.WriteLine();
            Console.WriteLine($"Component Roles: {pack.ComponentRoles.Count}");
            Console.WriteLine($"Naming - Namespace: {pack.NamingConventions.Namespace}");
            Console.WriteLine($"Naming - FilePath:  {pack.NamingConventions.FilePath}");
        });

        return command;
    }

    internal static RulePack ResolveRulesFromFile(IServiceProvider provider)
    {
        const string rulesPath = ".architecture/rules.yaml";

        if (!File.Exists(rulesPath))
        {
            throw new FileNotFoundException(
                "No .architecture/rules.yaml found. Run 'arch init' first.",
                rulesPath);
        }

        var loader = provider.GetRequiredService<IRulePackLoader>();
        var resolver = provider.GetRequiredService<IRulePackResolver>();

        var userPack = loader.LoadFromFile(rulesPath);
        return resolver.Resolve(userPack, Directory.GetCurrentDirectory());
    }
}
