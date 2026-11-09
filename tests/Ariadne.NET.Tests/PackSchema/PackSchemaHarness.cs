using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ariadne.NET.RuleLoader;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Ariadne.NET.Tests.PackSchema;

/// <summary>Validates pack YAML against the published JSON Schema and compares the verdict with the loader.</summary>
internal static partial class PackSchemaHarness
{
    private static readonly Lazy<JsonSchema> Schema = new(() =>
        JsonSchema.FromText(File.ReadAllText(Path.Combine(RepoRoot(), "docs-site", "static", "schema", "pack.schema.json"))));

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "Ariadne.NET.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found above " + AppContext.BaseDirectory);
    }

    public static JsonNode YamlToJson(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return Convert(stream.Documents[0].RootNode)!;
    }

    public static IReadOnlyList<string> SchemaErrors(string yaml)
    {
        var instance = YamlToJson(yaml);
        var result = Schema.Value.Evaluate(System.Text.Json.JsonSerializer.SerializeToElement(instance), new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid)
        {
            return [];
        }

        var errors = (result.Details ?? [])
            .Where(static d => d.Errors is { Count: > 0 })
            .SelectMany(static d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .ToList();
        return errors.Count > 0 ? errors : ["invalid"];
    }

    public static bool LoaderAccepts(string yaml) => new YamlRulePackLoader().LoadFromString(yaml).IsSuccess;

    /// <summary>True when the pack loads and also passes the cross-reference check the resolver runs on a merged pack.</summary>
    public static bool ReferencesResolve(string yaml)
    {
        var loaded = new YamlRulePackLoader().LoadFromString(yaml);
        return loaded.IsSuccess && RulePackReferenceCheck.Check(loaded.Value).IsSuccess;
    }

    private static JsonNode? Convert(YamlNode node) => node switch
    {
        YamlMappingNode map => ToObject(map),
        YamlSequenceNode seq => new JsonArray(seq.Children.Select(Convert).ToArray()),
        YamlScalarNode scalar => ToScalar(scalar),
        _ => throw new InvalidOperationException("Unsupported YAML node " + node.GetType().Name),
    };

    private static JsonObject ToObject(YamlMappingNode map)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in map.Children)
        {
            obj[((YamlScalarNode)key).Value!] = Convert(value);
        }

        return obj;
    }

    // Plain scalars are typed like YAML 1.2 core schema resolvers do, which is what a YAML language server sees.
    private static JsonNode? ToScalar(YamlScalarNode scalar)
    {
        var text = scalar.Value ?? string.Empty;
        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(text);
        }

        switch (text)
        {
            case "" or "~" or "null" or "Null" or "NULL":
                return null;
            case "true" or "True" or "TRUE":
                return JsonValue.Create(true);
            case "false" or "False" or "FALSE":
                return JsonValue.Create(false);
        }

        if (IntegerPattern().IsMatch(text))
        {
            return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole)
                ? JsonValue.Create(whole)
                : JsonValue.Create(text);
        }

        if (FloatPattern().IsMatch(text))
        {
            return JsonValue.Create(double.Parse(text, CultureInfo.InvariantCulture));
        }

        return JsonValue.Create(text);
    }

    [GeneratedRegex("^[-+]?\\d+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IntegerPattern();

    [GeneratedRegex("^[-+]?(\\.\\d+|\\d+(\\.\\d*)?)([eE][-+]?\\d+)?$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FloatPattern();
}
