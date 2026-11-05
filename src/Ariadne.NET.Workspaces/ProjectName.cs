using Microsoft.CodeAnalysis;

namespace Ariadne.NET.Workspaces;

/// <summary>Reads the project name and target framework from a workspace project.</summary>
internal static class ProjectName
{
    /// <summary>
    /// MSBuildWorkspace names each target framework of a multi-targeting project <c>Name(tfm)</c>, where <c>Name</c> is the project
    /// file's name. Only that form is split, so a project file named <c>Lib (Legacy).csproj</c> keeps its whole name.
    /// </summary>
    public static (string Name, string? TargetFramework) Split(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var name = project.Name;
        if (project.FilePath is { Length: > 0 } filePath)
        {
            var file = Path.GetFileNameWithoutExtension(filePath);
            if (!string.Equals(name, file, StringComparison.Ordinal)
                && name.Length > file.Length + 2
                && name.StartsWith(file, StringComparison.Ordinal)
                && name[file.Length] == '('
                && name.EndsWith(')'))
            {
                return (file, name[(file.Length + 1)..^1]);
            }
        }

        return (name, null);
    }
}
