namespace Ariadne.NET.Tests.Shared;

/// <summary>
/// Preconditions for tests that look for a solution or an <c>.architecture</c> folder from a temp folder upward: a stray
/// file in an ancestor of the temp folder would be found instead and fail the test for an unrelated reason.
/// </summary>
internal static class StrayFiles
{
    /// <summary>Throws, naming the stray file or folder, when an ancestor of <paramref name="folder"/> holds a solution file.</summary>
    internal static void AssertNoSolutionAbove(string folder)
    {
        foreach (var dir in Ancestors(folder))
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Test precondition failed: '{file}' sits above the temp folder '{folder}', so a search upward would find it. Remove it to run this test.");
                }
            }
        }
    }

    /// <summary>Throws, naming the stray folder, when an ancestor of <paramref name="folder"/> holds an <c>.architecture</c> folder.</summary>
    internal static void AssertNoArchitectureFolderAbove(string folder)
    {
        foreach (var dir in Ancestors(folder))
        {
            var stray = Path.Combine(dir, ".architecture");
            if (Directory.Exists(stray))
            {
                throw new InvalidOperationException($"Test precondition failed: '{stray}' sits above the temp folder '{folder}', so a search upward would find it. Remove it to run this test.");
            }
        }
    }

    private static IEnumerable<string> Ancestors(string folder)
    {
        for (var dir = new DirectoryInfo(folder); dir is not null; dir = dir.Parent)
        {
            if (dir.Exists)
            {
                yield return dir.FullName;
            }
        }
    }
}
