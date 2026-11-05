using Ariadne.NET.Abstractions;
using Ariadne.NET.Sync;

using ZeroAlloc.Results;
using ZeroAlloc.Telemetry;

using static Ariadne.NET.Telemetry.AriadneTelemetry;

namespace Ariadne.NET.Telemetry;

/// <summary>
/// The sync operation, instrumented: ZeroAlloc.Telemetry generates <c>SyncOperationsInstrumented</c>, which opens one span
/// per import and records <see cref="AriadneTelemetry.OperationDuration"/>. The span wraps every git command of the
/// import, the resilience proxy's retries included, so git commands get no span of their own. A failed Result marks the
/// span as an error and tags the span and the histogram with its <see cref="AriadneTelemetry.ErrorCode"/>; a caller's
/// cancellation is not an error. The import's url is never a tag: it can carry credentials.
/// </summary>
[Instrument(SourceName)]
internal interface ISyncOperations
{
    /// <summary>Fetches <paramref name="import"/> into <paramref name="cacheDir"/> with <paramref name="git"/>.</summary>
    [Trace(SyncImportSpan, ErrorWhen = "IsFailure")]
    [TraceTagFromResult(ErrorCode, "Error.Code", When = "IsFailure")]
    [Histogram(OperationDuration, Unit = Seconds, Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 180 })]
    [MetricTagConstant(Operation, SyncImportOperation)]
    [MetricTagFromResult(ErrorCode, "Error.Code", When = "IsFailure")]
    Task<Result<SyncedImport, AriadneError>> SyncImportAsync(
        [TraceTag(ImportName, "Name")] ImportDefinition import,
        string cacheDir,
        bool noCache,
        IGitClient git,
        CancellationToken ct);
}
