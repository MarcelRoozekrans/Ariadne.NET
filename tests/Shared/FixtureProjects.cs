using System.Diagnostics;
using System.Globalization;

namespace Ariadne.NET.Tests.Shared;

/// <summary>
/// Locates the fixture solutions under <c>tests/fixtures</c> and restores them once, so tests can load them with MSBuildWorkspace.
/// </summary>
internal static class FixtureProjects
{
    private const string LockFileName = ".restore.lock";
    private const string MarkerFileName = ".restore-marker";

    private static readonly string[] RestoreTargets =
    [
        "clean/Shop.slnx",
        "broken/Shop.slnx",
        "multitarget/Multi.csproj",
        "compile-error/Broken.csproj",
        "generator/Gen.App/Gen.App.csproj",
    ];

    private static readonly Lazy<string> RootFolder = new(FindRoot);
    private static readonly Lazy<bool> Restored = new(() =>
    {
        RestoreUnderLock();
        return true;
    });

    /// <summary>The <c>tests/fixtures</c> folder.</summary>
    public static string Root => RootFolder.Value;

    /// <summary>Combines <paramref name="parts"/> onto <see cref="Root"/>.</summary>
    public static string Path(params string[] parts) =>
        System.IO.Path.Combine([Root, .. parts]);

    /// <summary>
    /// Restores every fixture except <c>unrestored/</c> and builds the generator fixture. Runs once per process, and under an
    /// exclusive file lock, so test assemblies that run in parallel take turns.
    /// </summary>
    public static void EnsureRestored() => _ = Restored.Value;

    /// <summary>Takes the lock, then restores unless the marker says the fixtures are already restored. Bypasses the once-per-process guard.</summary>
    internal static void RestoreUnderLock()
    {
        using var fileLock = TakeLock(Path(LockFileName));

        var stamp = LatestSourceWrite().ToString(CultureInfo.InvariantCulture);
        var markerPath = Path(MarkerFileName);
        if (File.Exists(markerPath)
            && string.Equals(File.ReadAllText(markerPath), stamp, StringComparison.Ordinal)
            && File.Exists(Path("clean", "Shop.Domain", "obj", "project.assets.json")))
        {
            return;
        }

        foreach (var target in RestoreTargets)
        {
            Dotnet("restore", Path(target.Split('/')));
        }

        Dotnet("build", Path("generator", "Gen.Generator", "Gen.Generator.csproj"), "--no-restore");
        File.WriteAllText(markerPath, stamp);
    }

    private static FileStream TakeLock(string lockPath)
    {
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
    }

    private static long LatestSourceWrite()
    {
        var latest = 0L;
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            var relative = System.IO.Path.GetRelativePath(Root, file);
            var segments = relative.Split(System.IO.Path.DirectorySeparatorChar);
            if (Array.Exists(segments, IsBuildOutput) || segments[0].StartsWith(".restore", StringComparison.Ordinal))
            {
                continue;
            }

            latest = Math.Max(latest, File.GetLastWriteTimeUtc(file).Ticks);
        }

        return latest;
    }

    private static bool IsBuildOutput(string segment) =>
        string.Equals(segment, "bin", StringComparison.Ordinal) || string.Equals(segment, "obj", StringComparison.Ordinal);

    private static void Dotnet(string verb, string target, params string[] extra)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add(verb);
        info.ArgumentList.Add(target);
        foreach (var arg in extra)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start dotnet.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet {verb} {target} exited with code {process.ExitCode}.{Environment.NewLine}{stdout.GetAwaiter().GetResult()}{stderr.GetAwaiter().GetResult()}");
        }
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "src", "Ariadne.NET.slnx")))
            {
                return System.IO.Path.Combine(dir.FullName, "tests", "fixtures");
            }
        }

        throw new DirectoryNotFoundException($"No folder with src/Ariadne.NET.slnx above '{AppContext.BaseDirectory}'.");
    }
}
