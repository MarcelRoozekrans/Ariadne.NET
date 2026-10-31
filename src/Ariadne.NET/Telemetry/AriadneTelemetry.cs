namespace Ariadne.NET.Telemetry;

/// <summary>
/// The names the engine's spans and metrics use. Subscribe to <see cref="SourceName"/> as both the ActivitySource and the
/// Meter, for example with OpenTelemetry's <c>AddSource</c> and <c>AddMeter</c>.
/// </summary>
/// <remarks>
/// The spans are <c>ariadne.load</c>, <c>ariadne.resolve</c>, <c>ariadne.get_pack</c> and <c>ariadne.sync_import</c>. A span
/// whose operation returns a failed Result has the Error status and carries <see cref="ErrorCode"/>. Tags carry pack and
/// import names, never file contents, YAML text or urls.
/// </remarks>
public static class AriadneTelemetry
{
    /// <summary>The ActivitySource and Meter name.</summary>
    public const string SourceName = "Ariadne.NET";

    /// <summary>The duration histogram every operation records, in seconds.</summary>
    public const string OperationDuration = "ariadne.operation.duration";

    /// <summary>The metric tag naming the operation: <c>load</c>, <c>resolve</c>, <c>get_pack</c> or <c>sync_import</c>.</summary>
    public const string Operation = "ariadne.operation";

    /// <summary>The span and metric tag holding the <c>AriadneErrorCode</c> of a failed operation.</summary>
    public const string ErrorCode = "ariadne.error.code";

    /// <summary>The span tag holding the name of the pack a resolve or get-pack call works on.</summary>
    public const string PackName = "ariadne.pack.name";

    /// <summary>The span tag holding the name of the import a sync call fetches.</summary>
    public const string ImportName = "ariadne.import.name";

    internal const string LoadSpan = "ariadne.load";
    internal const string ResolveSpan = "ariadne.resolve";
    internal const string GetPackSpan = "ariadne.get_pack";
    internal const string SyncImportSpan = "ariadne.sync_import";

    internal const string LoadOperation = "load";
    internal const string ResolveOperation = "resolve";
    internal const string GetPackOperation = "get_pack";
    internal const string SyncImportOperation = "sync_import";

    internal const string Seconds = "s";

    /// <summary>
    /// Gets the bucket boundaries of <see cref="OperationDuration"/>, in seconds: a pack loads in milliseconds, and a sync
    /// can take up to three git attempts of a minute each. The instrumented interfaces repeat them as literals, since an
    /// attribute argument cannot read a property; a test checks the instruments' advice against these.
    /// </summary>
    internal static ReadOnlySpan<double> DurationBuckets => [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 180];
}
