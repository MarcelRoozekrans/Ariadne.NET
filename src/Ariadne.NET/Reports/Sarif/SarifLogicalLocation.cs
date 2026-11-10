using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifLogicalLocation(
    [property: JsonPropertyOrder(1)] string Name,
    [property: JsonPropertyOrder(2)] string? Kind);
