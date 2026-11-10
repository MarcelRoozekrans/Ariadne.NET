using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

internal sealed record SarifTool([property: JsonPropertyOrder(1)] SarifDriver Driver);
