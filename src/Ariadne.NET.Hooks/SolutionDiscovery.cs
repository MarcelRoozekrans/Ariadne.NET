namespace Ariadne.NET.Hooks;

/// <summary>Finds the solution a project's agent should validate.</summary>
public static class SolutionDiscovery
{
    private static readonly string[] Extensions = [".slnx", ".sln", ".slnf"];

    /// <summary>
    /// Looks in <paramref name="root"/>, then in <c>root/src</c>; the first folder that has any solution file decides. In a
    /// folder it prefers <c>.slnx</c>, then <c>.sln</c>, and takes a <c>.slnf</c> only when there is neither. Returns null when
    /// that folder has none or several of the preferred kind, or when neither folder has a solution.
    /// </summary>
    public static string? Find(string root)
    {
        ArgumentNullException.ThrowIfNull(root);

        foreach (var folder in new[] { root, Path.Combine(root, "src") })
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var files = Directory.GetFiles(folder);
            foreach (var extension in Extensions)
            {
                string? found = null;
                var count = 0;
                foreach (var file in files)
                {
                    if (string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase))
                    {
                        found = file;
                        count++;
                    }
                }

                if (count > 0)
                {
                    return count == 1 ? found : null;
                }
            }
        }

        return null;
    }
}
