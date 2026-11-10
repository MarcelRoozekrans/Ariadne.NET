using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifDriver(
    [property: JsonPropertyOrder(1)] string Name,
    [property: JsonPropertyOrder(2)] string Version,
    [property: JsonPropertyOrder(3)] string InformationUri,
    [property: JsonPropertyOrder(4)] IReadOnlyList<SarifRule> Rules);
