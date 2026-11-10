using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifResult(
    [property: JsonPropertyOrder(1)] string RuleId,
    [property: JsonPropertyOrder(2)] int RuleIndex,
    [property: JsonPropertyOrder(3)] string Level,
    [property: JsonPropertyOrder(4)] SarifMessage Message,
    [property: JsonPropertyOrder(5)] IReadOnlyList<SarifLocation> Locations,
    [property: JsonPropertyOrder(6)] SarifResultProperties Properties);
