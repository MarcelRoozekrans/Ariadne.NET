using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ariadne.NET.Tests.Shared;

/// <summary>
/// Locates the fixture solutions under <c>tests/fixtures</c> and restores them once, so tests can load them with MSBuildWorkspace.
/// </summary>
internal static class FixtureProjects
{
    internal const string LockFileName = ".restore.lock";
    private const string MarkerFileName = ".restore-marker";
    internal const string ParityLockFileName = ".parity-build.lock";
    private const string ParityMarkerFileName = ".parity-build-marker";

    private static readonly string[] RestoreTargets =
    [
        "clean/Shop.slnx",
        "broken/Shop.slnx",
        "twopacks/Shop.slnx",
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
    private static readonly Lock ParityGuard = new();
    private static bool _parityBuilt;

    /// <summary>The <c>tests/fixtures</c> folder.</summary>
    public static string Root => RootFolder.Value;

    /// <summary>The <c>tests/fixtures/archunit-parity</c> folder, which holds the ArchUnitNET parity fixtures.</summary>
    public static string ParityRoot => Path("archunit-parity");

    /// <summary>
    /// The Debug <c>net10.0</c> output folder of the parity fixture project in <paramref name="projectFolder"/>, where the
    /// parity build puts its DLL.
    /// </summary>
    public static string ParityOutput(string projectFolder) =>
        System.IO.Path.Combine(projectFolder, "bin", "Debug", "net10.0");

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
    /// Builds the ArchUnitNET parity fixtures, which ArchUnitNET reads as compiled assemblies. Runs once per process, under its
    /// own exclusive file lock, and skips the build when the marker matches the <see cref="SourceStamp"/> of the sources and
    /// every fixture DLL exists. Separate from <see cref="EnsureRestored"/>, so the other test projects never build the parity fixtures.
    /// </summary>
    internal static void EnsureParityBuilt()
    {
        lock (ParityGuard)
        {
            if (_parityBuilt)
            {
                return;
            }

            BuildParityUnderLock(ParityRoot);
            _parityBuilt = true;
        }
    }

    private static void BuildParityUnderLock(string parityRoot)
    {
        using var fileLock = TakeLock(System.IO.Path.Combine(parityRoot, ParityLockFileName));

        var stamp = SourceStamp(parityRoot);
        var markerPath = System.IO.Path.Combine(parityRoot, ParityMarkerFileName);
        if (File.Exists(markerPath)
            && string.Equals(File.ReadAllText(markerPath), stamp, StringComparison.Ordinal)
            && ParityOutputsExist(parityRoot))
        {
            return;
        }

        Dotnet("build", System.IO.Path.Combine(parityRoot, "Parity.slnx"));
        File.WriteAllText(markerPath, stamp);
    }

    /// <summary>True when every fixture kind under <paramref name="parityRoot"/> has its Debug <c>net10.0</c> DLL.</summary>
    private static bool ParityOutputsExist(string parityRoot)
    {
        foreach (var project in Directory.EnumerateFiles(parityRoot, "*.csproj", SearchOption.AllDirectories))
        {
            var dll = System.IO.Path.Combine(
                ParityOutput(System.IO.Path.GetDirectoryName(project)!), System.IO.Path.GetFileNameWithoutExtension(project) + ".dll");
            if (!File.Exists(dll))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Takes the lock, then restores unless the marker says the fixtures are already restored. Bypasses the once-per-process
    /// guard. <paramref name="root"/> is a fixtures folder: the real one, or a copy that a test can damage.
    /// </summary>
    internal static void RestoreUnderLock(string root)
    {
        using var fileLock = TakeLock(System.IO.Path.Combine(root, LockFileName));

        var stamp = SourceStamp(root);
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
                // Only folders that hold a project: a fixture may also carry an .architecture folder.
                foreach (var project in Directory.EnumerateDirectories(folder))
                {
                    if (Directory.GetFiles(project, "*.csproj").Length == 0)
                    {
                        continue;
                    }

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

    /// <summary>
    /// Identifies the sources under <paramref name="root"/>: the newest write time, the file count and a hash of the sorted
    /// relative paths, so an edit, an added file, a deleted file and a rename all change it. Build output, lock and marker
    /// files do not count.
    /// </summary>
    internal static string SourceStamp(string root)
    {
        var latest = 0L;
        var paths = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = System.IO.Path.GetRelativePath(root, file);
            var segments = relative.Split(System.IO.Path.DirectorySeparatorChar);
            if (Array.Exists(segments, IsBuildOutput) || IsLockOrMarker(segments[^1]))
            {
                continue;
            }

            latest = Math.Max(latest, File.GetLastWriteTimeUtc(file).Ticks);
            paths.Add(string.Join('/', segments));
        }

        paths.Sort(StringComparer.Ordinal);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', paths))));
        return string.Create(CultureInfo.InvariantCulture, $"{latest}:{paths.Count}:{hash}");
    }

    /// <summary>
    /// True for the lock and marker files of the restore and the parity build, wherever they sit: they change on every run, so
    /// counting them would make each marker invalidate the other.
    /// </summary>
    /// <remarks>Lock and marker files only live at fixture roots, so matching the file name alone cannot skip a source file.</remarks>
    internal static bool IsLockOrMarker(string fileName) =>
        fileName.StartsWith(".restore", StringComparison.Ordinal) || fileName.StartsWith(".parity-", StringComparison.Ordinal);

    private static bool IsBuildOutput(string segment) =>
        string.Equals(segment, "bin", StringComparison.Ordinal) || string.Equals(segment, "obj", StringComparison.Ordinal);

    /// <summary>Runs <c>dotnet</c> with <paramref name="verb"/>, <paramref name="target"/> and <paramref name="extra"/>; throws with its output when it fails or takes over five minutes.</summary>
    internal static void Dotnet(string verb, string target, params string[] extra)
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
