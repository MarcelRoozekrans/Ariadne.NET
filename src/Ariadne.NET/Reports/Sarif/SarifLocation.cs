using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifLocation(
    [property: JsonPropertyOrder(1)] SarifPhysicalLocation? PhysicalLocation,
    [property: JsonPropertyOrder(2)] IReadOnlyList<SarifLogicalLocation>? LogicalLocations);
