namespace Ariadne.NET.Hooks;

/// <summary>
/// Finds the project a hook works on. The <c>cwd</c> of Claude Code's hook input follows the agent's <c>cd</c>, so it may be
/// a subfolder of the project, or a folder outside it.
/// </summary>
internal static class ProjectRoot
{
    private const string EnvironmentVariable = "CLAUDE_PROJECT_DIR";

    /// <summary>
    /// The nearest folder from <paramref name="cwd"/> upwards that has <c>.architecture/rules.yaml</c>; else the folder
    /// <c>CLAUDE_PROJECT_DIR</c> names, when it is set and exists; else <paramref name="cwd"/> itself. A null
    /// <paramref name="cwd"/> stands for the process's working directory. <paramref name="getEnvironmentVariable"/> reads
    /// the environment and defaults to the process environment.
    /// </summary>
    internal static string Resolve(string? cwd, Func<string, string?>? getEnvironmentVariable = null)
    {
        var start = cwd ?? Directory.GetCurrentDirectory();
        for (var folder = start; !string.IsNullOrEmpty(folder); folder = Path.GetDirectoryName(folder))
        {
            if (File.Exists(Path.Combine(folder, ".architecture", "rules.yaml")))
            {
                return folder;
            }
        }

        var fromEnvironment = (getEnvironmentVariable ?? Environment.GetEnvironmentVariable)(EnvironmentVariable);
        return !string.IsNullOrWhiteSpace(fromEnvironment) && Directory.Exists(fromEnvironment) ? fromEnvironment : start;
    }
}
