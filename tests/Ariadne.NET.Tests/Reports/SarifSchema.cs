using System.Text.Json;
using Json.Schema;

namespace Ariadne.NET.Tests.Reports;

/// <summary>Validates SARIF JSON against the vendored SARIF 2.1.0 schema.</summary>
internal static class SarifSchema
{
    // The SARIF schema declares draft-04, which JsonSchema.Net does not ship. The schema uses no draft-04
    // only keywords, so it is evaluated as draft-07 registered under the draft-04 id: no network fetch.
    private static readonly Lazy<JsonSchema> Schema = new(() =>
    {
        var dialects = new DialectRegistry();
        dialects.Register(Dialect.Draft07.With(new Uri("http://json-schema.org/draft-04/schema#"), null!));
        return JsonSchema.FromText(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Reports", "sarif-schema-2.1.0.json")),
            new BuildOptions { DialectRegistry = dialects });
    });

    /// <summary>Returns one line per schema violation, empty when the document is valid.</summary>
    public static IReadOnlyList<string> Validate(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = Schema.Value.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid)
        {
            return [];
        }

        var errors = (result.Details ?? [])
            .Where(static d => d.Errors is { Count: > 0 })
            .SelectMany(static d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .ToList();
        return errors.Count > 0 ? errors : ["The document does not match the SARIF 2.1.0 schema."];

    }
}
