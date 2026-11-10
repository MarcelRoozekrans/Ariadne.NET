using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Json;

internal sealed record JsonSkippedProject(
    [property: JsonPropertyOrder(1)] string Name,
    [property: JsonPropertyOrder(2)] string Reason);
