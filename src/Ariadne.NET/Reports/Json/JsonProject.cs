using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Json;

internal sealed record JsonProject(
    [property: JsonPropertyOrder(1)] string Name,
    [property: JsonPropertyOrder(2)] IReadOnlyList<string> TargetFrameworks);
