using System.Diagnostics;
using System.Globalization;

namespace Ariadne.NET.Tests.Shared;

/// <summary>
/// Locates the fixture solutions under <c>tests/fixtures</c> and restores them once, so tests can load them with MSBuildWorkspace.
/// </summary>
internal static class FixtureProjects
{
    internal const string LockFileName = ".restore.lock";
    private const string MarkerFileName = ".restore-marker";

    private static readonly string[] RestoreTargets =
    [
        "clean/Shop.slnx",
        "broken/Shop.slnx",
        "multitarget/Multi.csproj",
        "fourlayer/Shop.csproj",
        "compile-error/Broken.csproj",
        "generator/Gen.App/Gen.App.csproj",
        "daedalus/clean/Daedalus.slnx",
        "daedalus/broken/Daedalus.slnx",
        "packages/clean/Packages.slnx",
        "packages/broken/Packages.slnx",
    ];

    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(5);
    private static readonly Lazy<string> RootFolder = new(FindRoot);
    private static readonly Lock RestoreGuard = new();
    private static bool _restored;

    /// <summary>The <c>tests/fixtures</c> folder.</summary>
    public static string Root => RootFolder.Value;

    /// <summary>Combines <paramref name="parts"/> onto <see cref="Root"/>.</summary>
    public static string Path(params string[] parts) =>
        System.IO.Path.Combine([Root, .. parts]);

    /// <summary>
    /// Restores every fixture except <c>unrestored/</c> and builds the generator fixture. Runs once per process, and under an
    /// exclusive file lock, so test assemblies that run in parallel take turns.
    /// </summary>
    public static void EnsureRestored()
    {
        lock (RestoreGuard)
        {
            if (_restored)
            {
                return;
            }

            RestoreUnderLock(Root);
            _restored = true;
        }
    }

    /// <summary>
    /// Takes the lock, then restores unless the marker says the fixtures are already restored. Bypasses the once-per-process
    /// guard. <paramref name="root"/> is a fixtures folder: the real one, or a copy that a test can damage.
    /// </summary>
    internal static void RestoreUnderLock(string root)
    {
        using var fileLock = TakeLock(System.IO.Path.Combine(root, LockFileName));

        var stamp = LatestSourceWrite(root).ToString(CultureInfo.InvariantCulture);
        var markerPath = System.IO.Path.Combine(root, MarkerFileName);
        if (File.Exists(markerPath)
            && string.Equals(File.ReadAllText(markerPath), stamp, StringComparison.Ordinal)
            && OutputsExist(root))
        {
            return;
        }

        foreach (var target in RestoreTargets)
        {
            Dotnet("restore", System.IO.Path.Combine([root, .. target.Split('/')]));
        }

        Dotnet("build", System.IO.Path.Combine(root, "generator", "Gen.Generator", "Gen.Generator.csproj"), "--no-restore");
        File.WriteAllText(markerPath, stamp);
    }

    /// <summary>True when every restore target has its assets file and the generator has been built.</summary>
    internal static bool OutputsExist(string root)
    {
        foreach (var assets in AssetsFiles(root))
        {
            if (!File.Exists(assets))
            {
                return false;
            }
        }

        return File.Exists(GeneratorDll(root));
    }

    /// <summary>The <c>obj/project.assets.json</c> of each restore target, one per project folder of a solution.</summary>
    internal static IEnumerable<string> AssetsFiles(string root)
    {
        foreach (var target in RestoreTargets)
        {
            var targetPath = System.IO.Path.Combine([root, .. target.Split('/')]);
            var folder = System.IO.Path.GetDirectoryName(targetPath)!;
            if (targetPath.EndsWith(".slnx", StringComparison.Ordinal))
            {
                foreach (var project in Directory.EnumerateDirectories(folder))
                {
                    yield return System.IO.Path.Combine(project, "obj", "project.assets.json");
                }
            }
            else
            {
                yield return System.IO.Path.Combine(folder, "obj", "project.assets.json");
            }
        }
    }

    internal static string GeneratorDll(string root) =>
        System.IO.Path.Combine(root, "generator", "Gen.Generator", "bin", "Debug", "netstandard2.0", "Gen.Generator.dll");

    /// <summary>Takes an exclusive lock on <paramref name="lockPath"/>, retrying while another holder has it, for at most five minutes.</summary>
    internal static FileStream TakeLock(string lockPath)
    {
        var deadline = DateTime.UtcNow + LockTimeout;
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex) when (IsSharingViolation(ex))
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException($"Could not take the lock file '{lockPath}' within {LockTimeout.TotalMinutes} minutes.", ex);
                }

                Thread.Sleep(50);
            }
        }
    }

    /// <summary>
    /// True when <paramref name="ex"/> says another holder has the file: ERROR_SHARING_VIOLATION on Windows, and on Unix the
    /// errno that .NET passes through as the HResult of a failed advisory lock, EAGAIN, which is 11 on Linux and 35 on macOS.
    /// Anything else, such as a missing folder or a full disk, is a real error and not a reason to wait.
    /// </summary>
    internal static bool IsSharingViolation(IOException ex) =>
        OperatingSystem.IsWindows()
            ? ex.HResult == unchecked((int)0x80070020)
            : ex.HResult is 11 or 35;

    private static long LatestSourceWrite(string root)
    {
        var latest = 0L;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = System.IO.Path.GetRelativePath(root, file);
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
        if (!process.WaitForExit(ProcessTimeout))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException(
                $"dotnet {verb} {target} did not finish within {ProcessTimeout.TotalMinutes} minutes.{Environment.NewLine}{stdout.GetAwaiter().GetResult()}{stderr.GetAwaiter().GetResult()}");
        }

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
            var fixtures = System.IO.Path.Combine(dir.FullName, "tests", "fixtures");
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "src", "Ariadne.NET.slnx")) && Directory.Exists(fixtures))
            {
                return fixtures;
            }
        }

        throw new DirectoryNotFoundException($"No folder with src/Ariadne.NET.slnx and tests/fixtures above '{AppContext.BaseDirectory}'.");
    }
}
