using Ariadne.NET.Abstractions;

namespace Ariadne.NET.RuleLoader;

/// <summary>
/// Which exception keys a rule kind takes. A bannedPackages exception names a project and a uniqueNames exception names a
/// type; the other keys could never match. The loader and the merger, which amends inherited rules, share these rules.
/// </summary>
internal static class ExceptionKeyRules
{
    /// <summary>Why the exception key does not apply to the rule kind, or null when it does.</summary>
    public static string? Rejection(RuleKind kind, string key) => kind switch
    {
        RuleKind.BannedPackages when key is "types" or "namespaces" || Array.IndexOf(PackYamlReader.TraitKeys, key) >= 0 =>
            "a package belongs to a project, so the exception of a bannedPackages rule names it by 'assemblies'",
        RuleKind.UniqueNames when key is "assemblies" =>
            "an exception of a uniqueNames rule excuses a type's name, so it takes 'types' or 'namespaces'",
        _ => null,
    };

    /// <summary>The YAML keys an exception built in code sets, in the order the loader lists them.</summary>
    public static IEnumerable<string> UsedKeys(RuleException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        (string Key, bool Used)[] keys =
        [
            ("namespaces", exception.Namespaces.Count > 0),
            ("assemblies", exception.Assemblies.Count > 0),
            ("types", exception.Types.Count > 0),
            ("names", exception.Names.Count > 0),
            ("kinds", exception.Kinds.Count > 0),
            ("accessibility", exception.Accessibility.Count > 0),
            ("assignableTo", exception.AssignableTo.Count > 0),
            ("sealed", exception.Sealed is not null),
            ("abstract", exception.Abstract is not null),
            ("static", exception.Static is not null),
            ("nested", exception.Nested is not null),
        ];
        return keys.Where(static k => k.Used).Select(static k => k.Key);
    }

    /// <summary>The selector keys an exception of this kind may use, in the order the loader lists them.</summary>
    public static string[] Allowed(RuleKind kind) => kind switch
    {
        RuleKind.BannedPackages => ["assemblies"],
        RuleKind.UniqueNames => ["namespaces", "types", .. PackYamlReader.TraitKeys],
        _ => ["namespaces", "assemblies", "types", .. PackYamlReader.TraitKeys],
    };
}
