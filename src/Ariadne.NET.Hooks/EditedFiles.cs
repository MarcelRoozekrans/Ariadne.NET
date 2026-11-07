using System.Text;

namespace Ariadne.NET.Hooks;

/// <summary>
/// The C# files the agent changed in a session. Every edit is its own small file in a folder per session, below a folder
/// per user in the temp folder, so parallel hook processes never write to the same file. Session ids are reduced to
/// <c>[A-Za-z0-9_-]</c>, so two different ids can share a folder, for instance <c>a/b</c> and <c>a_b</c>.
/// </summary>
public static class EditedFiles
{
    private const string RecordExtension = ".rec";
    private const string TempExtension = ".tmp";

    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    /// <summary>Adds <paramref name="path"/> to the session's list. Duplicates are dropped on Read. Paths compare case-sensitively.</summary>
    public static void Record(string sessionId, string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // The record is written under a temp name and then moved, so a reader never sees half a record. The name starts with
        // the time, so Read gives the first-recorded order, and ends in a guid, so two writers never share a name.
        // It throws IOException, UnauthorizedAccessException or ArgumentException when the record cannot be written, for
        // instance for an unwritable folder or an over-long session id.
        var folder = SessionFolder(sessionId);
        CreatePrivateDirectory(folder);
        var name = $"{DateTime.UtcNow.Ticks:D19}-{Guid.NewGuid():N}";
        var temp = Path.Combine(folder, name + TempExtension);
        try
        {
            File.WriteAllText(temp, path, new UTF8Encoding(false));
            File.Move(temp, Path.Combine(folder, name + RecordExtension));
        }
        catch
        {
            TryDelete(temp);
            throw;
        }

        Prune(MaxAge);
    }

    /// <summary>The recorded paths of the session in the order they were first recorded, empty when there are none.</summary>
    public static IReadOnlyList<string> Read(string sessionId)
    {
        var records = ReadRecords(sessionId);
        var paths = new List<string>(records.Count);
        foreach (var record in records)
        {
            paths.Add(record.Path);
        }

        return paths;
    }

    /// <summary>Forgets every recorded path of the session.</summary>
    public static void Clear(string sessionId)
    {
        foreach (var file in RecordFiles(SessionFolder(sessionId)))
        {
            DeleteRecord(file);
        }
    }

    /// <summary>Forgets only the records <paramref name="seen"/> holds, so an edit recorded after the read stays.</summary>
    internal static void Remove(IReadOnlyList<EditedRecord> seen)
    {
        ArgumentNullException.ThrowIfNull(seen);
        foreach (var record in seen)
        {
            DeleteRecord(record.File);
        }
    }

    /// <summary>The records of the session with their files, paths de-duplicated, in the order they were first recorded.</summary>
    internal static IReadOnlyList<EditedRecord> ReadRecords(string sessionId)
    {
        var folder = SessionFolder(sessionId);
        var files = RecordFiles(folder);
        Array.Sort(files, StringComparer.Ordinal);

        var records = new List<EditedRecord>(files.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            string path;
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                path = reader.ReadToEnd();
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                continue; // Cleared by another process since the listing.
            }

            if (path.Length > 0 && seen.Add(path))
            {
                records.Add(new EditedRecord(file, path));
            }
        }

        return records;
    }

    /// <summary>
    /// Removes session folders nobody touched for <paramref name="maxAge"/>. Best effort: a hook must not fail over a
    /// stale folder, so anything that goes wrong is ignored.
    /// </summary>
    internal static void Prune(TimeSpan maxAge)
    {
        try
        {
            var cutoff = DateTime.UtcNow - maxAge;
            foreach (var folder in Directory.EnumerateDirectories(Folder))
            {
                if (Directory.GetLastWriteTimeUtc(folder) < cutoff)
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Another process may be using or removing the folder; the next record tries again.
        }
    }

    /// <summary>The folder of this user, <c>ariadne-hooks-&lt;user&gt;</c> in the temp folder, so users on a shared machine never meet.</summary>
    internal static string Folder => Path.Combine(Path.GetTempPath(), "ariadne-hooks-" + SafeName(Environment.UserName));

    internal static string SessionFolder(string sessionId) => Path.Combine(Folder, SafeName(sessionId));

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

    private static string[] RecordFiles(string folder) =>
        Directory.Exists(folder) ? Directory.GetFiles(folder, "*" + RecordExtension) : [];

    // Other users can neither list nor read the recorded paths. Windows keeps the inherited access of the user's temp folder.
    private static void CreatePrivateDirectory(string folder)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(folder);
            return;
        }

        const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        Directory.CreateDirectory(Folder, Private);
        Directory.CreateDirectory(folder, Private);
    }

    // A record another process or a reader holds open is gone on the next Clear; on Windows the readers share Delete.
    private static void DeleteRecord(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (FileNotFoundException)
        {
            // Already cleared.
        }
        catch (DirectoryNotFoundException)
        {
            // Already pruned.
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The original failure is the one to report; pruning removes a stray temp file with its folder.
        }
    }
}
