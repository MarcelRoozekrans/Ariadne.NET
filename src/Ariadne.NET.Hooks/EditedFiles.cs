using System.Text;

namespace Ariadne.NET.Hooks;

/// <summary>The C# files the agent changed in a session, kept in one text file per session in the temp folder.</summary>
public static class EditedFiles
{
    /// <summary>Adds <paramref name="path"/> to the session's list unless it is already there. Paths compare case-sensitively.</summary>
    public static void Record(string sessionId, string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var known = Read(sessionId);
        if (known.Contains(path, StringComparer.Ordinal))
        {
            return;
        }

        Directory.CreateDirectory(Folder);
        File.AppendAllText(FileOf(sessionId), path + "\n", Encoding.UTF8);
    }

    /// <summary>The recorded paths of the session in the order they were first recorded, empty when there are none.</summary>
    public static IReadOnlyList<string> Read(string sessionId)
    {
        var file = FileOf(sessionId);
        if (!File.Exists(file))
        {
            return [];
        }

        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(file, Encoding.UTF8))
        {
            if (line.Length > 0 && seen.Add(line))
            {
                paths.Add(line);
            }
        }

        return paths;
    }

    /// <summary>Forgets the session's recorded paths.</summary>
    public static void Clear(string sessionId)
    {
        var file = FileOf(sessionId);
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    private static string Folder => Path.Combine(Path.GetTempPath(), "ariadne-hooks");

    private static string FileOf(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        var name = new StringBuilder(sessionId.Length + 4);
        foreach (var c in sessionId)
        {
            name.Append(c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-' ? c : '_');
        }

        if (name.Length == 0)
        {
            name.Append('_');
        }

        return Path.Combine(Folder, name + ".txt");
    }
}
