using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;

namespace Ariadne.NET.Hooks;

/// <summary>Builds the text the agent gets at session start.</summary>
public static class SessionContext
{
    /// <summary>
    /// Warnings, header, layers, rules, roles and naming conventions, in that order. Over <paramref name="maxChars"/>, whole lines
    /// go from the end of roles, then rules, then layers, and a final line says how many.
    /// </summary>
    public static string Build(RulePack pack, IReadOnlyList<string> versions, IReadOnlyList<string> warnings, int maxChars = 10_000)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(warnings);

        var head = new List<string>();
        head.AddRange(warnings.Select(w => $"Warning: {w}"));
        var detail = versions.Count > 0 ? $"{pack.Name}; {string.Join(", ", versions)}" : pack.Name;
        head.Add($"Ariadne rules ({detail})");

        var layers = pack.Layers.Select(l => $"  {RulePackDescriber.DescribeLayer(l)}").ToList();
        var rules = pack.Rules
            .SelectMany(r => RulePackDescriber.DescribeRule(r).Select((line, i) => (i == 0 ? "  " : "    ") + line))
            .ToList();
        var roles = pack.ComponentRoles.Select(r => $"  {r.Name} (layer {r.Layer})").ToList();
        var naming = new List<string>
        {
            $"Naming: namespace {pack.NamingConventions.Namespace}; file path {pack.NamingConventions.FilePath}",
        };

        var dropped = 0;
        var text = Join(head, layers, rules, roles, naming, dropped);
        foreach (var section in new[] { roles, rules, layers })
        {
            while (text.Length > maxChars && section.Count > 0)
            {
                section.RemoveAt(section.Count - 1);
                dropped++;
                text = Join(head, layers, rules, roles, naming, dropped);
            }
        }

        return text.Length > maxChars ? text[..maxChars] : text;
    }

    private static string Join(
        List<string> head, List<string> layers, List<string> rules, List<string> roles, List<string> naming, int dropped)
    {
        var lines = new List<string>(head);
        AddSection(lines, "Layers:", layers);
        AddSection(lines, "Rules:", rules);
        AddSection(lines, "Component roles:", roles);
        lines.AddRange(naming);
        if (dropped > 0)
        {
            lines.Add($"Truncated: {dropped} more lines. Run 'ariadne rules' for all of them.");
        }

        return string.Join('\n', lines);
    }

    private static void AddSection(List<string> lines, string title, List<string> body)
    {
        if (body.Count == 0)
        {
            return;
        }

        lines.Add(title);
        lines.AddRange(body);
    }
}
