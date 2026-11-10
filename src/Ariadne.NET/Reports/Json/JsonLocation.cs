using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Json;

internal sealed record JsonLocation(
    [property: JsonPropertyOrder(1)] string Path,
    [property: JsonPropertyOrder(2)] int Line,
    [property: JsonPropertyOrder(3)] int Column);
