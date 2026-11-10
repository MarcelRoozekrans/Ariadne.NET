using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifResultProperties(
    [property: JsonPropertyOrder(1)] string Pack,
    [property: JsonPropertyOrder(2)] IReadOnlyList<string> TargetFrameworks);
