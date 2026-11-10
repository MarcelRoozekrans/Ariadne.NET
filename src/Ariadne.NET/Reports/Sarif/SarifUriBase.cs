using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifUriBase([property: JsonPropertyOrder(1)] string Uri);
