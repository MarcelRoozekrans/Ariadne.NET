using System.Text.Json.Serialization;
using ZeroAlloc.Serialisation;

namespace Ariadne.NET.Reports.Sarif;

/// <summary>The root of a SARIF 2.1.0 log.</summary>
[ZeroAllocSerializable(SerializationFormat.SystemTextJson)]
internal sealed record SarifLog(
    [property: JsonPropertyName("$schema"), JsonPropertyOrder(1)] string Schema,
    [property: JsonPropertyOrder(2)] string Version,
    [property: JsonPropertyOrder(3)] IReadOnlyList<SarifRun> Runs);
