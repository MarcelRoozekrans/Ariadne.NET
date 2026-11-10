using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifArtifactLocation(
    [property: JsonPropertyOrder(1)] string Uri,
    [property: JsonPropertyOrder(2)] string? UriBaseId);
