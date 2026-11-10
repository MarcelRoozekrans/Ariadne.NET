using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifRegion(
    [property: JsonPropertyOrder(1)] int StartLine,
    [property: JsonPropertyOrder(2)] int StartColumn);
