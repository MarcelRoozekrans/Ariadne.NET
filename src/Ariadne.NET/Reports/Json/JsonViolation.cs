using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Json;

internal sealed record JsonViolation(
    [property: JsonPropertyOrder(1)] string RuleId,
    [property: JsonPropertyOrder(2)] string Pack,
    [property: JsonPropertyOrder(3)] string Kind,
    [property: JsonPropertyOrder(4)] string SourceType,
    [property: JsonPropertyOrder(5)] string Message,
    [property: JsonPropertyOrder(6)] string Because,
    [property: JsonPropertyOrder(7)] string? Target,
    [property: JsonPropertyOrder(8)] JsonLocation? Location,
    [property: JsonPropertyOrder(9)] int ReferenceCount,
    [property: JsonPropertyOrder(10)] IReadOnlyList<string> TargetFrameworks);
