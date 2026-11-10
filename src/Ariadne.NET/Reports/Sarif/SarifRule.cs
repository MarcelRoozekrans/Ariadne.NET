using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifRule(
    [property: JsonPropertyOrder(1)] string Id,
    [property: JsonPropertyOrder(2)] SarifMessage ShortDescription,
    [property: JsonPropertyOrder(3)] SarifMessage Help,
    [property: JsonPropertyOrder(4)] SarifRuleProperties Properties);
