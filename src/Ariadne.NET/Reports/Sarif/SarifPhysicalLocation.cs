using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifPhysicalLocation(
    [property: JsonPropertyOrder(1)] SarifArtifactLocation ArtifactLocation,
    [property: JsonPropertyOrder(2)] SarifRegion Region);
