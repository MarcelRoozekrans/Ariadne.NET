using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Sarif;

/// <summary>The source-generated System.Text.Json metadata for the SARIF report.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SarifLog))]
internal sealed partial class SarifReportContext : JsonSerializerContext;
