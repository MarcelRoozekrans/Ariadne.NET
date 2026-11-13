namespace Ariadne.NET.Tests.Analysis;

/// <summary>
/// Measures what code allocates on the test thread. Interim: measures with <see cref="GC.GetAllocatedBytesForCurrentThread"/>
/// because the ZeroAlloc.TestHelpers source fails this repo's analyzers, tracked as ZeroAlloc-Net/ZeroAlloc.TestHelpers#106.
/// Switch to AllocationGate once that issue ships.
/// </summary>
internal static class Allocations
{
    private const int Rounds = 5;

    /// <summary>
    /// Runs <paramref name="body"/> <paramref name="iterations"/> times per round, for five rounds, and returns the fewest bytes
    /// one round allocated. Now and then the runtime does one-time work on the test thread, such as starting the tiered JIT's
    /// background worker, and its bytes land in the round it happens in. An allocation the code makes on each call lands in
    /// every round, so the fewest bytes are zero only when the code allocates nothing.
    /// </summary>
    /// <param name="body">The calls to measure. Build what it captures before calling this, so building it is not measured.</param>
    /// <param name="iterations">How often one round runs <paramref name="body"/>.</param>
    /// <returns>The fewest bytes allocated by one round.</returns>
    public static long PerRound(Action body, int iterations = 1_000)
    {
        var fewest = long.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < iterations; i++)
            {
                body();
            }

            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return fewest;
    }
}
