using Ariadne.NET.Abstractions;
using Ariadne.NET.RuleEngine;

namespace Ariadne.NET.Hooks;

/// <summary>Builds the text the agent gets at session start.</summary>
public static class SessionContext
{
    /// <summary>
    /// Warnings, header, layers, rules, roles and naming conventions, in that order. Over <paramref name="maxChars"/>, whole
    /// roles, then whole rules, then whole layers go from the end, then the naming line and the warnings from the end, and a
    /// final line says how many lines went. The result never exceeds <paramref name="maxChars"/>; every line is capped at
    /// <see cref="TextLimit.MaxLine"/> characters, and the header is shortened last of all. A cap below the length of the
    /// final line itself (about 70 characters) can only be met by cutting that line.
    /// </summary>
    public static string Build(RulePack pack, IReadOnlyList<string> versions, IReadOnlyList<string> warnings, int maxChars = 10_000)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(warnings);

        var warningLines = warnings.Select(w => Line($"Warning: {w}")).ToList();
        var detail = versions.Count > 0 ? $"{pack.Name}; {string.Join(", ", versions)}" : pack.Name;
        var header = Line($"Ariadne rules ({detail})");
        var layers = new Section("Layers:", [.. pack.Layers.Select(l => new List<string> { Line($"  {RulePackDescriber.DescribeLayer(l)}") })]);
        var rules = new Section("Rules:", [.. pack.Rules.Select(r => RulePackDescriber.DescribeRule(r)
            .Select((line, i) => Line((i == 0 ? "  " : "    ") + line)).ToList())]);
        var roles = new Section("Component roles:", [.. pack.ComponentRoles.Select(r => new List<string> { Line($"  {r.Name} (layer {r.Layer})") })]);
        var naming = Line($"Naming: namespace {pack.NamingConventions.Namespace}; file path {pack.NamingConventions.FilePath}");

        var size = Size(warningLines) + header.Length + 1 + layers.Size + rules.Size + roles.Size + naming.Length + 1;
        var droppable = warningLines.Count + layers.LineCount + rules.LineCount + roles.LineCount + 1;
        var keepNaming = true;

        // The size counts every line with its newline; the final text has one newline fewer, which keeps a spare char.
        if (size > maxChars)
        {
            var budget = maxChars - TruncatedLine(droppable).Length - 1;
            var dropped = 0;
            foreach (var section in new[] { roles, rules, layers })
            {
                while (size > budget && section.Units.Count > 0)
                {
                    dropped += section.DropLast(ref size);
                }
            }

            if (size > budget)
            {
                keepNaming = false;
                size -= naming.Length + 1;
                dropped++;
            }

            while (size > budget && warningLines.Count > 0)
            {
                size -= warningLines[^1].Length + 1;
                warningLines.RemoveAt(warningLines.Count - 1);
                dropped++;
            }

            var truncated = TruncatedLine(dropped);
            var headerRoom = maxChars - truncated.Length - 1 - (size - header.Length - 1);
            if (header.Length > headerRoom)
            {
                header = TextLimit.Shorten(header, Math.Max(headerRoom, 0));
            }

            var lines = Assemble(warningLines, header, layers, rules, roles, keepNaming ? naming : null);
            lines.RemoveAll(l => l.Length == 0);
            lines.Add(truncated);
            return TextLimit.Shorten(string.Join('\n', lines), maxChars);
        }

        return string.Join('\n', Assemble(warningLines, header, layers, rules, roles, naming));
    }

    private static List<string> Assemble(
        List<string> warnings, string header, Section layers, Section rules, Section roles, string? naming)
    {
        var lines = new List<string>(warnings) { header };
        layers.AddTo(lines);
        rules.AddTo(lines);
        roles.AddTo(lines);
        if (naming is not null)
        {
            lines.Add(naming);
        }

        return lines;
    }

    private static string Line(string text) => TextLimit.Shorten(text);

    private static string TruncatedLine(int count) =>
        $"Truncated: {count} more lines. Run 'ariadne rules' for all of them.";

    private static int Size(List<string> lines) => lines.Sum(l => l.Length + 1);

    // A section is a title and units that stay together: one layer, one rule with its because line, or one role.
    private sealed class Section(string title, List<List<string>> units)
    {
        public List<List<string>> Units { get; } = units;

        public int LineCount => Units.Sum(u => u.Count);

        public int Size => Units.Count == 0 ? 0 : title.Length + 1 + Units.Sum(u => u.Sum(l => l.Length + 1));

        // Removes the last unit, and the title with the last one; returns the number of lines dropped.
        public int DropLast(ref int size)
        {
            var unit = Units[^1];
            Units.RemoveAt(Units.Count - 1);
            size -= unit.Sum(l => l.Length + 1);
            if (Units.Count == 0)
            {
                size -= title.Length + 1;
            }

            return unit.Count;
        }

        public void AddTo(List<string> lines)
        {
            if (Units.Count == 0)
            {
                return;
            }

            lines.Add(title);
            foreach (ref readonly var unit in System.Runtime.InteropServices.CollectionsMarshal.AsSpan(Units))
            {
                lines.AddRange(unit);
            }
        }
    }
}
