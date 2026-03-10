using System.CommandLine;

namespace ArchGov.Host.Cli.Commands;

public static class RolesCommand
{
    public static Command Create(IServiceProvider provider)
    {
        var command = new Command("roles", "Print component roles from resolved rules");

        command.SetAction(_ =>
        {
            var pack = RulesCommand.ResolveRulesFromFile(provider);

            Console.WriteLine("Component Roles");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine();
            Console.WriteLine($"  {"Role",-20} {"Layer",-20} {"Kind",-10} {"Interface",-10} {"Description"}");
            Console.WriteLine($"  {new string('-', 20)} {new string('-', 20)} {new string('-', 10)} {new string('-', 10)} {new string('-', 20)}");

            foreach (var role in pack.ComponentRoles)
            {
                Console.WriteLine(
                    $"  {role.Name,-20} {role.Layer,-20} {role.TypeKind,-10} {(role.RequiresInterface ? "Yes" : "No"),-10} {role.Description}");
            }
        });

        return command;
    }
}
