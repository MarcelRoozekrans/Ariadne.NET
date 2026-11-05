using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;
using Ariadne.NET.RuleLoader;

using ZeroAlloc.Results;
using ZeroAlloc.Telemetry;

using static Ariadne.NET.Telemetry.AriadneTelemetry;

namespace Ariadne.NET.Telemetry;

/// <summary>
/// The pack operations, instrumented: ZeroAlloc.Telemetry generates <c>PackOperationsInstrumented</c>, which opens one span
/// per call and records <see cref="AriadneTelemetry.OperationDuration"/>. A failed Result marks the span as an error and
/// tags the span and the histogram with its <see cref="AriadneTelemetry.ErrorCode"/>. The public loader, registry and
/// resolver call through it, passing their collaborators, so <see cref="PackOperations"/> holds the one implementation.
/// </summary>
/// <remarks>
/// Neither load operation tags anything identifying: YAML text is content, and a file path can name the user's home
/// directory. The pack's name is only known once it has parsed.
/// </remarks>
[Instrument(SourceName)]
internal interface IPackOperations
{
    /// <summary>Parses and checks a rule pack held in <paramref name="yaml"/>.</summary>
    [Trace(LoadSpan, ErrorWhen = "IsFailure")]
    [TraceTagFromResult(ErrorCode, "Error.Code", When = "IsFailure")]
    [Histogram(OperationDuration, Unit = Seconds, Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 180 })]
    [MetricTagConstant(Operation, LoadOperation)]
    [MetricTagFromResult(ErrorCode, "Error.Code", When = "IsFailure")]
    Result<RulePack, AriadneError> LoadFromString(string yaml);

    /// <summary>Reads, parses and checks the rule pack at <paramref name="filePath"/>.</summary>
    [Trace(LoadSpan, ErrorWhen = "IsFailure")]
    [TraceTagFromResult(ErrorCode, "Error.Code", When = "IsFailure")]
    [Histogram(OperationDuration, Unit = Seconds, Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 180 })]
    [MetricTagConstant(Operation, LoadOperation)]
    [MetricTagFromResult(ErrorCode, "Error.Code", When = "IsFailure")]
    Result<RulePack, AriadneError> LoadFromFile(string filePath);

    /// <summary>Returns the built-in pack <paramref name="name"/> from <paramref name="catalog"/>, loading it on first use.</summary>
    [Trace(GetPackSpan, ErrorWhen = "IsFailure")]
    [TraceTagFromResult(ErrorCode, "Error.Code", When = "IsFailure")]
    [Histogram(OperationDuration, Unit = Seconds, Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 180 })]
    [MetricTagConstant(Operation, GetPackOperation)]
    [MetricTagFromResult(ErrorCode, "Error.Code", When = "IsFailure")]
    Result<RulePack, AriadneError> GetPack([TraceTag(PackName)] string name, BuiltinPackCatalog catalog);

    /// <summary>Resolves <paramref name="userPack"/>'s <c>extends</c> references and merges them under it.</summary>
    [Trace(ResolveSpan, ErrorWhen = "IsFailure")]
    [TraceTagFromResult(ErrorCode, "Error.Code", When = "IsFailure")]
    [Histogram(OperationDuration, Unit = Seconds, Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 180 })]
    [MetricTagConstant(Operation, ResolveOperation)]
    [MetricTagFromResult(ErrorCode, "Error.Code", When = "IsFailure")]
    Result<RulePack, AriadneError> Resolve(
        [TraceTag(PackName, "Name")] RulePack userPack,
        string? workingDirectory,
        IBuiltinPackRegistry registry,
        IRulePackLoader loader,
        IRulePackMerger merger);
}
