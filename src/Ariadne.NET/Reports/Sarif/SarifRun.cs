using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifRun(
    [property: JsonPropertyOrder(1)] SarifTool Tool,
    [property: JsonPropertyOrder(2)] IReadOnlyDictionary<string, SarifUriBase> OriginalUriBaseIds,
    [property: JsonPropertyOrder(3)] IReadOnlyList<SarifResult> Results);
