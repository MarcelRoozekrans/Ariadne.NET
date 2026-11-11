using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifMessage([property: JsonPropertyOrder(1)] string Text);
