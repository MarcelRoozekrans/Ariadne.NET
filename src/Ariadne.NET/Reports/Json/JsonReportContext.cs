using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ariadne.NET.Reports.Json;

/// <summary>The source-generated System.Text.Json metadata for the JSON report.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JsonReportDocument))]
internal sealed partial class JsonReportContext : JsonSerializerContext;
