using System.Text.Json.Serialization;
using ZeroAlloc.Serialisation;

namespace Ariadne.NET.Reports.Json;

/// <summary>The root of Ariadne's own JSON report.</summary>
[ZeroAllocSerializable(SerializationFormat.SystemTextJson)]
internal sealed record JsonReportDocument(
    [property: JsonPropertyOrder(1)] int Schema,
    [property: JsonPropertyOrder(2)] JsonTool Tool,
    [property: JsonPropertyOrder(3)] int TypesChecked,
    [property: JsonPropertyOrder(4)] IReadOnlyList<JsonProject> Projects,
    [property: JsonPropertyOrder(5)] IReadOnlyList<JsonSkippedProject> SkippedProjects,
    [property: JsonPropertyOrder(6)] IReadOnlyList<JsonViolation> Violations);
