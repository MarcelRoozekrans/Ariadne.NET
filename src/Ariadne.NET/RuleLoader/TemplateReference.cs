namespace Ariadne.NET.RuleLoader;

/// <summary>
/// Parses template expressions such as <c>clean-architecture+cqrs</c>, where <c>+</c> composes built-in packs.
/// </summary>
public static class TemplateReference
{
    private const string BuiltinPrefix = "builtin://";

    public static IList<string> ParseNames(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var names = template.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (names.Length == 0)
        {
            throw new ArgumentException($"Template expression '{template}' contains no template names.", nameof(template));
        }

        return names;
    }

    public static IList<string> ToExtends(string template)
    {
        return ParseNames(template).Select(name => BuiltinPrefix + name).ToList();
    }
}
