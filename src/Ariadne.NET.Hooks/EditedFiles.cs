using System.Text;

namespace Ariadne.NET.Hooks;

/// <summary>
/// The C# files the agent changed in a session, kept in one text file per session in the temp folder. Session ids are
/// reduced to <c>[A-Za-z0-9_-]</c>, so two different ids can share a file, for instance <c>a/b</c> and <c>a_b</c>.
/// </summary>
public static class EditedFiles
{
    /// <summary>Adds <paramref name="path"/> to the session's list unless it is already there. Paths compare case-sensitively.</summary>
    public static void Record(string sessionId, string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Parallel tool calls run parallel hook processes on the same file, so this is one append that shares the file.
        // Duplicates are dropped on Read. It throws IOException, UnauthorizedAccessException or ArgumentException when the
        // file cannot be written, for instance for an unwritable folder or an over-long session id.
        Directory.CreateDirectory(Folder);
        using var stream = new FileStream(PathOf(sessionId), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        var bytes = Encoding.UTF8.GetBytes(path + "\n");
        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>The recorded paths of the session in the order they were first recorded, empty when there are none.</summary>
    public static IReadOnlyList<string> Read(string sessionId)
    {
        var file = PathOf(sessionId);
        if (!File.Exists(file))
        {
            return [];
        }

        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        using var reader = new StreamReader(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), Encoding.UTF8);
        while (reader.ReadLine() is { } line)
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
        var file = PathOf(sessionId);
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    internal static string Folder => Path.Combine(Path.GetTempPath(), "ariadne-hooks");

    internal static string PathOf(string sessionId) => Path.Combine(Folder, SafeName(sessionId) + ".txt");

    /// <summary>The file name of a session without extension: every character outside <c>[A-Za-z0-9_-]</c> becomes <c>_</c>.</summary>
    internal static string SafeName(string sessionId)
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

        return name.ToString();
    }
}
