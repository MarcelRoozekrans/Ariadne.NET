using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifRuleProperties([property: JsonPropertyOrder(1)] string Pack);
