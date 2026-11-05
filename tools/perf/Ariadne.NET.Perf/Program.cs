using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;
using Ariadne.NET.Workspaces;
using Microsoft.Extensions.DependencyInjection;

// Usage: Ariadne.NET.Perf <path> [--runs N]
// Run it from the repository root: the rules come from .architecture/rules.yaml in the current directory, as the CLI does.
if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Usage: Ariadne.NET.Perf <path> [--runs N]");
    return 2;
}

var path = args[0];
var runs = 5;
for (var i = 1; i < args.Length; i++)
{
    if (string.Equals(args[i], "--runs", StringComparison.Ordinal)
        && i + 1 < args.Length
        && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out runs)
        && runs > 0)
    {
        i++;
        continue;
    }

    Console.Error.WriteLine("Usage: Ariadne.NET.Perf <path> [--runs N], with N a positive integer");
    return 2;
}

const string rulesPath = ".architecture/rules.yaml";
if (!File.Exists(rulesPath))
{
    Console.Error.WriteLine($"No {rulesPath} found in {Directory.GetCurrentDirectory()}; run from the repository root.");
    return 2;
}

using var services = new ServiceCollection().AddAriadneNETServices().AddAriadneNETWorkspaces().BuildServiceProvider();
var userPack = services.GetRequiredService<IRulePackLoader>().LoadFromFile(rulesPath);
if (userPack.IsFailure)
{
    Console.Error.WriteLine(userPack.Error.Message);
    return 2;
}

var resolved = services.GetRequiredService<IRulePackResolver>().Resolve(userPack.Value, Directory.GetCurrentDirectory());
if (resolved.IsFailure)
{
    Console.Error.WriteLine(resolved.Error.Message);
    return 2;
}

var loader = services.GetRequiredService<ISolutionLoader>();
var validator = services.GetRequiredService<IArchitectureValidator>();

var loadMs = new double[runs];
var checkMs = new double[runs];
var allocated = new long[runs];
var projects = 0;
var types = 0;
var violations = 0;

// Run -1 is a warm-up: it pays for JIT and cold caches, and is neither stored nor printed.
for (var run = -1; run < runs; run++)
{
    var loadWatch = Stopwatch.StartNew();
    var loaded = await loader.LoadAsync(path).ConfigureAwait(false);
    loadWatch.Stop();
    if (loaded.IsFailure)
    {
        Console.Error.WriteLine(loaded.Error.Message);
        return 2;
    }

    using (loaded.Value)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetTotalAllocatedBytes(true);
        var checkWatch = Stopwatch.StartNew();
        var report = validator.Validate(resolved.Value, loaded.Value.Projects);
        checkWatch.Stop();
        var after = GC.GetTotalAllocatedBytes(true);

        if (run < 0)
        {
            continue;
        }

        loadMs[run] = loadWatch.Elapsed.TotalMilliseconds;
        checkMs[run] = checkWatch.Elapsed.TotalMilliseconds;
        allocated[run] = after - before;
        projects = report.Projects.Count;
        types = report.TypesChecked;
        violations = report.Violations.Count;
    }

    Console.WriteLine(string.Create(
        CultureInfo.InvariantCulture,
        $"run {run + 1}: load {loadMs[run]:F0} ms, check {checkMs[run]:F1} ms, check allocated {allocated[run]:N0} bytes"));
}

Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"projects {projects}, types {types}, violations {violations}"));
Console.WriteLine(string.Create(
    CultureInfo.InvariantCulture,
    $"median ({runs} runs): load {Median(loadMs):F0} ms, check {Median(checkMs):F1} ms, check allocated {Median(allocated):N0} bytes"));
return 0;

static T Median<T>(T[] values)
    where T : INumber<T>
{
    var sorted = (T[])values.Clone();
    Array.Sort(sorted);
    var mid = sorted.Length / 2;
    return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / T.CreateChecked(2);
}
