using System.Runtime.InteropServices;

using Ariadne.NET.Abstractions;
using Ariadne.NET.Analysis;

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

using ZeroAlloc.Results;

namespace Ariadne.NET.RuleLoader;

/// <summary>
/// Reads a schema 2 pack strictly: unknown keys, missing required keys, wrong value types, empty selectors and invalid
/// patterns are all collected in one pass, each at the 1-based line and column of the offending node.
/// </summary>
internal static class PackYamlReader
{
    private const string V1Hint =
        "'schema: 2' is required; this looks like a v1 pack. See https://marcelroozekrans.github.io/Ariadne.NET/how-it-works/rule-schema#migrating-from-v1";

    private const string CodeFeatureRulesKey = "codeFeatureRules";

    private const string CodeFeatureRulesMessage =
        "'codeFeatureRules' is not part of schema 2; typed code-feature rules arrive in a later release";

    private static readonly string[] PackKeys =
        ["schema", "name", "version", "author", "extends", "layers", "rules", "componentRoles", "namingConventions"];

    private static readonly string[] LayerKeys =
        ["name", "id", "because", "description", "namespaces", "assemblies", "allowedDependencies"];

    private static readonly string[] RetargetKeys = ["name", "namespaces", "assemblies"];

    private static readonly string[] RuleKeys =
        ["id", "because", "description", "forbid", "allowOnly", "require", "bannedPackages", "uniqueNames", "exceptions", "locked", "allowEmpty"];

    private static readonly string[] AmendmentKeys = ["id", "exceptions"];

    private static readonly string[] KindKeys = ["from", "to"];

    /// <summary>
    /// The type-level trait keys. They select by what a type is rather than where it lives, so they may join 'layers', and
    /// an exception takes them too.
    /// </summary>
    internal static readonly string[] TraitKeys =
        ["names", "kinds", "accessibility", "assignableTo", "sealed", "abstract", "static", "nested"];

    // The selector keys of a uniqueNames target, which does not take traits.
    private static readonly string[] PlainTargetKeys =
        ["layers", "namespaces", "assemblies", "types", "baseTypes", "interfaces", "attributes"];

    private static readonly string[] TargetKeys = [.. PlainTargetKeys, .. TraitKeys];

    // Members name APIs, so only the 'to' of forbid and allowOnly takes them.
    private static readonly string[] ToKeys = [.. PlainTargetKeys, "members", .. TraitKeys];

    // What 'is' and 'isNot' check against the type itself: every selector key except 'layers' and 'members'.
    private static readonly string[] ConditionKeys =
        ["namespaces", "assemblies", "types", "baseTypes", "interfaces", "attributes", .. TraitKeys];

    private static readonly string[] BannedPackagesKeys = ["packages", "from"];

    private static readonly string[] UniqueNamesKeys = ["for", "against"];

    private static readonly string[] RequireKeys = ["for", "layer", "kind", "accessibility", "implements", "is", "isNot"];

    // What a type must implement is matched by name only: its namespace, assembly or full name.
    private static readonly string[] ImplementsKeys = ["namespaces", "assemblies", "types"];

    private static readonly string[] OtherSelectorKeys = ["namespaces", "assemblies", "types", "baseTypes", "interfaces", "attributes", "members"];

    private static readonly string[] ExceptionKeys = ["namespaces", "assemblies", "types", .. TraitKeys, "because"];

    private const string RuleBodyKeys = "'forbid', 'allowOnly', 'require', 'bannedPackages' or 'uniqueNames'";

    private readonly record struct RuleBody(
        RuleKind Kind, TargetSelector From, TargetSelector To, RuleRequirement? Require, List<string> Packages, TargetSelector Against);

    private static readonly string[] RoleKeys =
    [
        "name", "layer", "typeKind", "requiresInterface", "interfaceRole", "accessibility", "description", "alternativeNames", "allowEmpty",
    ];

    private static readonly string[] NamingKeys = ["namespace", "filePath"];

    // kind and typeKind take the five kinds code can be built for; enum and delegate are only selectable, through kinds.
    private static readonly CSharpTypeKind[] BuildableTypeKinds =
        [CSharpTypeKind.Class, CSharpTypeKind.Interface, CSharpTypeKind.Record, CSharpTypeKind.Struct, CSharpTypeKind.RecordStruct];

    private static readonly string TypeKindNames = string.Join(", ", BuildableTypeKinds.Select(static kind => kind.ToString()));

    // The kinds trait selects every kind, spelled in camelCase and matched exactly, as the JSON Schema's enum does.
    private static readonly (string Name, CSharpTypeKind Kind)[] SelectableTypeKinds =
    [
        ("class", CSharpTypeKind.Class),
        ("interface", CSharpTypeKind.Interface),
        ("record", CSharpTypeKind.Record),
        ("struct", CSharpTypeKind.Struct),
        ("recordStruct", CSharpTypeKind.RecordStruct),
        ("enum", CSharpTypeKind.Enum),
        ("delegate", CSharpTypeKind.Delegate),
    ];

    /// <summary>The six accessibility keywords of the accessibility trait, matched exactly.</summary>
    internal static readonly string[] AccessibilityKeywords =
        ["public", "internal", "protected", "private", "protected internal", "private protected"];

    private static readonly string KindsNames = string.Join(", ", SelectableTypeKinds.Select(static k => $"'{k.Name}'"));

    private static readonly string AccessibilityNames = string.Join(", ", AccessibilityKeywords.Select(static k => $"'{k}'"));

    private static readonly string[] BooleanTraitKeys = ["sealed", "abstract", "static", "nested"];

    /// <summary>
    /// The most entries one read visits, counting a node again each time an alias reaches it. Aliases may nest, so a few
    /// kilobytes could otherwise expand to billions of entries.
    /// </summary>
    internal const int MaxEntries = 100_000;

    /// <summary>Reads one pack file.</summary>
    /// <param name="yaml">The file's text. A leading UTF-8 BOM is ignored; LF and CRLF give the same positions.</param>
    /// <returns>The pack, or every problem found, in document order.</returns>
    public static Result<RulePack, IReadOnlyList<PackProblem>> Read(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        var text = yaml.StartsWith('\uFEFF') ? yaml[1..] : yaml;
        using var reader = new StringReader(text);
        var parser = new DuplicateKeyFilter(new Parser(reader));
        var stream = new YamlStream();
        try
        {
            stream.Load(parser);
        }
        catch (YamlException ex)
        {
            var (line, column) = DuplicateKeyFilter.Position(ex.Start);
            // The parser's message may quote pack text, such as an anchor name, so it is shown like pack text.
            return Result<RulePack, IReadOnlyList<PackProblem>>.Failure([new PackProblem(line, column, PackText.Show(ex.Message))]);
        }

        return new Walk(parser.Duplicates).Read(stream);
    }

    private static bool IsNull(YamlScalarNode scalar) =>
        scalar.Style == ScalarStyle.Plain && scalar.Value is null or "" or "~" or "null" or "Null" or "NULL";

    private static bool IsTrait(string key) => Array.IndexOf(TraitKeys, key) >= 0;

    private static bool OnlyKeys(Dictionary<string, Field> fields, string[] keys)
    {
        foreach (var key in fields.Keys)
        {
            if (Array.IndexOf(keys, key) < 0)
            {
                return false;
            }
        }

        return true;
    }

    // True when no selector list holds a value; a list that was itself invalid has already been reported.
    private static bool NoneSelected(Dictionary<string, Field> fields, ReadOnlySpan<(string Key, List<string>? Items)> selectors)
    {
        foreach (var (key, items) in selectors)
        {
            if (items is { Count: > 0 } || (items is null && fields.ContainsKey(key)))
            {
                return false;
            }
        }

        return true;
    }

    // The traits read from one mapping. A list that was itself invalid is null and has already been reported.
    private readonly record struct Traits(
        List<string>? Names,
        List<CSharpTypeKind>? Kinds,
        List<string>? Accessibility,
        List<string>? AssignableTo,
        bool? Sealed,
        bool? Abstract,
        bool? Static,
        bool? Nested)
    {
        // A boolean selects once it is written, whether true or false; a list selects once it holds a value.
        public bool AnySelected(Dictionary<string, Field> fields) =>
            Selected(fields, "names", Names?.Count)
            || Selected(fields, "kinds", Kinds?.Count)
            || Selected(fields, "accessibility", Accessibility?.Count)
            || Selected(fields, "assignableTo", AssignableTo?.Count)
            || Array.Exists(BooleanTraitKeys, fields.ContainsKey);

        private static bool Selected(Dictionary<string, Field> fields, string key, int? count) =>
            count > 0 || (count is null && fields.ContainsKey(key));
    }

    private readonly record struct Field(YamlScalarNode Key, YamlNode Value)
    {
        public string Name => Key.Value ?? "";
    }

    /// <summary>One read of one file: the walk over the representation model and the problems it found.</summary>
    private sealed class Walk(Dictionary<(int Line, int Column), List<DuplicateKeyFilter.DuplicateKey>> duplicates)
    {
        private readonly List<PackProblem> _problems = [];
        private bool _hasExtends;
        private int _visited;
        private bool _exhausted;

        public Result<RulePack, IReadOnlyList<PackProblem>> Read(YamlStream stream)
        {
            if (stream.Documents.Count == 0)
            {
                _problems.Add(new PackProblem(1, 1, "the pack is empty"));
            }
            else if (stream.Documents.Count > 1)
            {
                Add(stream.Documents[1].RootNode, "the pack must be a single YAML document");
            }
            else if (stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                Add(stream.Documents[0].RootNode, "the pack must be a mapping");
            }
            else
            {
                var pack = ReadPack(root);
                if (pack is not null && _problems.Count == 0)
                {
                    return Result<RulePack, IReadOnlyList<PackProblem>>.Success(pack);
                }
            }

            // A node reached again through an alias reports the same problems again; each is kept once.
            PackProblem[] ordered = [.. _problems.Distinct().OrderBy(p => p.Line).ThenBy(p => p.Column)];
            return Result<RulePack, IReadOnlyList<PackProblem>>.Failure(ordered);
        }

        private RulePack? ReadPack(YamlMappingNode root)
        {
            // Without schema 2 the rest of the file has another meaning, so the schema problem is the only one.
            var schema = FindKey(root, "schema");
            if (schema is null)
            {
                Add(root, V1Hint);
                return null;
            }

            if (schema.Value.Value is not YamlScalarNode { Value: "2" })
            {
                Add(schema.Value.Key, "'schema' must be 2");
                return null;
            }

            const string where = "the pack";
            var fields = Fields(root, PackKeys, where, isPack: true);
            var name = RequiredString(fields, "name", where, root);
            var version = OptionalString(fields, "version", where);
            var author = OptionalString(fields, "author", where);
            var extends = StringList(fields, "extends", where);
            _hasExtends = extends is { Count: > 0 };

            var (layers, retargets) = ReadLayers(fields, where);
            var (rules, amendments) = ReadRules(fields, where);
            var roles = ReadRoles(fields, where);
            var naming = ReadNaming(fields, where);
            ReportUnvisitedDuplicates();
            if (name is null)
            {
                return null;
            }

            var defaults = new RulePack { Name = name };
            return new RulePack
            {
                Name = name,
                Schema = 2,
                Version = version ?? defaults.Version,
                Author = author ?? defaults.Author,
                Extends = extends ?? [],
                Layers = layers,
                LayerRetargets = retargets,
                Rules = rules,
                RuleAmendments = amendments,
                ComponentRoles = roles,
                NamingConventions = naming,
            };
        }

        private (List<LayerDefinition> Layers, List<LayerRetarget> Retargets) ReadLayers(Dictionary<string, Field> packFields, string packWhere)
        {
            var layers = new List<LayerDefinition>();
            var retargets = new List<LayerRetarget>();
            var items = Items(packFields, "layers", packWhere);
            for (var i = 0; i < items.Count && Visit(items[i]); i++)
            {
                ReadLayer(items[i], i, layers, retargets);
            }

            return (layers, retargets);
        }

        private (List<RuleDefinition> Rules, List<RuleAmendment> Amendments) ReadRules(Dictionary<string, Field> packFields, string packWhere)
        {
            var rules = new List<RuleDefinition>();
            var amendments = new List<RuleAmendment>();
            var items = Items(packFields, "rules", packWhere);
            for (var i = 0; i < items.Count && Visit(items[i]); i++)
            {
                ReadRule(items[i], i, rules, amendments);
            }

            return (rules, amendments);
        }

        private List<ComponentRoleDefinition> ReadRoles(Dictionary<string, Field> packFields, string packWhere)
        {
            var roles = new List<ComponentRoleDefinition>();
            var items = Items(packFields, "componentRoles", packWhere);
            for (var i = 0; i < items.Count && Visit(items[i]); i++)
            {
                ReadRole(items[i], i, roles);
            }

            return roles;
        }

        private void ReadLayer(YamlNode node, int index, List<LayerDefinition> layers, List<LayerRetarget> retargets)
        {
            if (node is not YamlMappingNode map)
            {
                Add(node, $"'layers[{index}]' in the pack must be a mapping");
                return;
            }

            var where = PeekString(map, "name") is { } known ? $"layer '{PackText.Show(known)}'" : $"layers[{index}]";
            var before = _problems.Count;
            var fields = Fields(map, LayerKeys, where);
            var name = RequiredString(fields, "name", where, map);
            var namespaces = PatternList(fields, "namespaces", where, Glob.Dotted);
            var assemblies = PatternList(fields, "assemblies", where, Glob.Flat);

            if ((fields.ContainsKey("namespaces") || fields.ContainsKey("assemblies")) && OnlyKeys(fields, RetargetKeys))
            {
                if (!_hasExtends)
                {
                    Add(map, $"{where} only changes selectors, which needs 'extends'");
                }

                if (_problems.Count == before && name is not null)
                {
                    retargets.Add(new LayerRetarget { Name = name, Namespaces = namespaces ?? [], Assemblies = assemblies ?? [] });
                }

                return;
            }

            var id = RequiredString(fields, "id", where, map);
            var because = RequiredString(fields, "because", where, map);
            var description = OptionalString(fields, "description", where);
            var allowed = StringList(fields, "allowedDependencies", where);
            if (NoneSelected(fields, [("namespaces", namespaces), ("assemblies", assemblies)]))
            {
                Add(map, $"{where} needs at least one of 'namespaces', 'assemblies'");
            }

            if (_problems.Count == before && name is not null && id is not null && because is not null)
            {
                layers.Add(new LayerDefinition
                {
                    Name = name,
                    Id = id,
                    Because = because,
                    Description = description ?? "",
                    Namespaces = namespaces ?? [],
                    Assemblies = assemblies ?? [],
                    AllowedDependencies = allowed ?? [],
                });
            }
        }

        private void ReadRule(YamlNode node, int index, List<RuleDefinition> rules, List<RuleAmendment> amendments)
        {
            if (node is not YamlMappingNode map)
            {
                Add(node, $"'rules[{index}]' in the pack must be a mapping");
                return;
            }

            var where = PeekString(map, "id") is { } known ? $"rule '{PackText.Show(known)}'" : $"rules[{index}]";
            var before = _problems.Count;
            var fields = Fields(map, RuleKeys, where);
            var id = RequiredString(fields, "id", where, map);
            var exceptionKind = fields.ContainsKey("bannedPackages") ? RuleKind.BannedPackages
                : fields.ContainsKey("uniqueNames") ? RuleKind.UniqueNames
                : RuleKind.Forbid;
            var exceptions = ReadExceptions(fields, where, exceptionKind);

            if (fields.ContainsKey("exceptions") && OnlyKeys(fields, AmendmentKeys))
            {
                if (!_hasExtends)
                {
                    Add(map, $"{where} only adds exceptions, which needs 'extends'");
                }

                if (_problems.Count == before && id is not null)
                {
                    amendments.Add(new RuleAmendment { Id = id, Exceptions = exceptions });
                }

                return;
            }

            var because = RequiredString(fields, "because", where, map);
            var description = OptionalString(fields, "description", where);
            var locked = OptionalBool(fields, "locked", where);
            var allowEmpty = OptionalBool(fields, "allowEmpty", where);

            var body = ReadRuleBody(fields, map, where);
            if (_problems.Count == before && id is not null && because is not null && body is { } read)
            {
                rules.Add(new RuleDefinition
                {
                    Id = id,
                    Because = because,
                    Description = description ?? "",
                    Kind = read.Kind,
                    From = read.From,
                    To = read.To,
                    Require = read.Require,
                    Packages = read.Packages,
                    Against = read.Against,
                    Exceptions = exceptions,
                    Locked = locked ?? false,
                    AllowEmpty = allowEmpty ?? false,
                });
            }
        }

        // Exactly one of forbid, allowOnly, require, bannedPackages or uniqueNames.
        private RuleBody? ReadRuleBody(Dictionary<string, Field> fields, YamlMappingNode map, string where)
        {
            var hasForbid = fields.TryGetValue("forbid", out var forbid);
            var hasAllowOnly = fields.TryGetValue("allowOnly", out var allowOnly);
            var hasRequire = fields.TryGetValue("require", out var require);
            var hasBanned = fields.TryGetValue("bannedPackages", out var banned);
            var hasUnique = fields.TryGetValue("uniqueNames", out var unique);
            var count = (hasForbid ? 1 : 0) + (hasAllowOnly ? 1 : 0) + (hasRequire ? 1 : 0) + (hasBanned ? 1 : 0) + (hasUnique ? 1 : 0);
            if (count != 1)
            {
                YamlNode at = count == 0
                    ? map
                    : Latest(hasForbid ? forbid.Key : null, hasAllowOnly ? allowOnly.Key : null, hasRequire ? require.Key : null, hasBanned ? banned.Key : null, hasUnique ? unique.Key : null);
                Add(at, $"{where} must have exactly one of {RuleBodyKeys}");
                return null;
            }

            if (hasRequire)
            {
                return ReadRequire(in require, where) is { } required
                    ? new RuleBody(RuleKind.Require, required.For, new TargetSelector(), required.Require, [], new TargetSelector())
                    : null;
            }

            if (hasBanned)
            {
                return ReadBannedPackages(in banned, where) is { } bannedRule
                    ? new RuleBody(RuleKind.BannedPackages, bannedRule.From, new TargetSelector(), null, bannedRule.Packages, new TargetSelector())
                    : null;
            }

            if (hasUnique)
            {
                return ReadUniqueNames(in unique, where) is { } uniqueRule
                    ? new RuleBody(RuleKind.UniqueNames, uniqueRule.For, new TargetSelector(), null, [], uniqueRule.Against)
                    : null;
            }

            return (hasForbid ? ReadKind(in forbid, where) : ReadKind(in allowOnly, where)) is { } pair
                ? new RuleBody(hasForbid ? RuleKind.Forbid : RuleKind.AllowOnly, pair.From, pair.To, null, [], new TargetSelector())
                : null;
        }

        private (List<string> Packages, TargetSelector From)? ReadBannedPackages(in Field field, string ruleWhere)
        {
            if (field.Value is not YamlMappingNode map)
            {
                Add(field.Key, $"'bannedPackages' in {ruleWhere} must be a mapping");
                return null;
            }

            var where = $"the bannedPackages of {ruleWhere}";
            var before = _problems.Count;
            var fields = Fields(map, BannedPackagesKeys, where);

            // Package ids are ordinary dotted names; they match case-insensitively when a project's packages are checked.
            var packages = PatternList(fields, "packages", where, Glob.Dotted);
            if (packages is { Count: 0 } || (packages is null && !fields.ContainsKey("packages")))
            {
                Add(fields.TryGetValue("packages", out var packagesField) ? packagesField.Key : map, $"{where} needs at least one entry in 'packages'");
            }

            var from = new TargetSelector();
            if (fields.TryGetValue("from", out var fromField))
            {
                if (fromField.Value is not YamlMappingNode fromMap)
                {
                    Add(fromField.Key, $"'from' in {where} must be a mapping");
                }
                else
                {
                    var fromWhere = $"the from of {ruleWhere}";
                    var fromFields = Fields(fromMap, ToKeys, fromWhere);
                    foreach (var (key, entry) in fromFields)
                    {
                        if (!string.Equals(key, "assemblies", StringComparison.Ordinal))
                        {
                            Add(entry.Key, $"'{key}' is not accepted in {fromWhere}; a package belongs to a project, so 'from' selects projects by 'assemblies' only");
                        }
                    }

                    from = new TargetSelector { Assemblies = PatternList(fromFields, "assemblies", fromWhere, Glob.Flat) ?? [] };
                }
            }

            return _problems.Count == before && packages is not null ? (packages, from) : null;
        }

        private (TargetSelector For, TargetSelector Against)? ReadUniqueNames(in Field field, string ruleWhere)
        {
            if (field.Value is not YamlMappingNode map)
            {
                Add(field.Key, $"'uniqueNames' in {ruleWhere} must be a mapping");
                return null;
            }

            var where = $"the uniqueNames of {ruleWhere}";
            var fields = Fields(map, UniqueNamesKeys, where);
            var subject = ReadTarget(fields, "for", where, ruleWhere, map, PlainTargetKeys);
            var against = ReadTarget(fields, "against", where, ruleWhere, map, PlainTargetKeys);
            return subject is not null && against is not null ? (subject, against) : null;
        }

        private (TargetSelector For, RuleRequirement Require)? ReadRequire(in Field field, string ruleWhere)
        {
            if (field.Value is not YamlMappingNode map)
            {
                Add(field.Key, $"'require' in {ruleWhere} must be a mapping");
                return null;
            }

            var where = $"the require of {ruleWhere}";
            var before = _problems.Count;
            var fields = Fields(map, RequireKeys, where);

            TargetSelector? subject = null;
            if (!fields.TryGetValue("for", out var forField))
            {
                Add(map, $"{where} has no 'for'");
            }
            else if (forField.Value is not YamlMappingNode forMap)
            {
                Add(forField.Key, $"'for' in {where} must be a mapping");
            }
            else
            {
                subject = ReadSelector(forMap, $"the for of {ruleWhere}", ruleWhere, TargetKeys);
            }

            var layer = fields.ContainsKey("layer") ? RequiredString(fields, "layer", where, map) : null;
            var kind = ReadTypeKind(fields, where, "kind");
            var accessibility = ReadAccessibility(fields, where);

            var implements = ReadRequireSelector(fields, "implements", where, ruleWhere, ImplementsKeys);
            var condition = ReadRequireSelector(fields, "is", where, ruleWhere, ConditionKeys);
            var negated = ReadRequireSelector(fields, "isNot", where, ruleWhere, ConditionKeys);

            if (!Array.Exists(RequireKeys, key => !string.Equals(key, "for", StringComparison.Ordinal) && fields.ContainsKey(key)))
            {
                Add(map, $"{where} needs at least one of 'layer', 'kind', 'accessibility', 'implements', 'is', 'isNot'");
            }

            return _problems.Count == before && subject is not null
                ? (subject, new RuleRequirement
                {
                    Layer = layer,
                    Kind = kind,
                    Accessibility = accessibility,
                    Implements = implements,
                    Is = condition,
                    IsNot = negated,
                })
                : null;
        }

        // 'implements', 'is' or 'isNot': an optional selector under the require, with its own key set.
        private TargetSelector? ReadRequireSelector(Dictionary<string, Field> fields, string key, string where, string ruleWhere, string[] keys)
        {
            if (!fields.TryGetValue(key, out var field))
            {
                return null;
            }

            if (field.Value is YamlMappingNode map)
            {
                return ReadSelector(map, $"the {key} of {ruleWhere}", ruleWhere, keys);
            }

            Add(field.Key, $"'{key}' in {where} must be a mapping");
            return null;
        }

        private string? ReadAccessibility(Dictionary<string, Field> fields, string where)
        {
            var value = OptionalString(fields, "accessibility", where);
            if (value is null)
            {
                return null;
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (normalized is "public" or "internal")
            {
                return normalized;
            }

            Add(fields["accessibility"].Key, $"accessibility '{PackText.Show(value)}' in {where} must be public or internal");
            return null;
        }

        private static YamlNode Latest(params YamlScalarNode?[] keys)
        {
            YamlScalarNode? latest = null;
            foreach (var key in keys)
            {
                if (key is not null && (latest is null || key.Start.Index > latest.Start.Index))
                {
                    latest = key;
                }
            }

            return latest!;
        }

        private (TargetSelector From, TargetSelector To)? ReadKind(in Field field, string ruleWhere)
        {
            if (field.Value is not YamlMappingNode map)
            {
                Add(field.Key, $"'{field.Name}' in {ruleWhere} must be a mapping");
                return null;
            }

            var where = $"the {field.Name} of {ruleWhere}";
            var fields = Fields(map, KindKeys, where);
            var from = ReadTarget(fields, "from", where, ruleWhere, map, TargetKeys);
            var to = ReadTarget(fields, "to", where, ruleWhere, map, ToKeys);
            return from is not null && to is not null ? (from, to) : null;
        }

        private TargetSelector? ReadTarget(
            Dictionary<string, Field> kindFields, string key, string kindWhere, string ruleWhere, YamlMappingNode parent, string[] keys)
        {
            if (!kindFields.TryGetValue(key, out var field))
            {
                Add(parent, $"{kindWhere} has no '{key}'");
                return null;
            }

            if (field.Value is not YamlMappingNode map)
            {
                Add(field.Key, $"'{key}' in {kindWhere} must be a mapping");
                return null;
            }

            return ReadSelector(map, $"the {key} of {ruleWhere}", ruleWhere, keys);
        }

        // One selector mapping. Only the given keys may appear; 'layers' cannot mix with code patterns, but may join traits.
        private TargetSelector? ReadSelector(YamlMappingNode map, string where, string ruleWhere, string[] keys)
        {
            var before = _problems.Count;
            var fields = Fields(map, keys, where);
            var listKeys = Array.FindAll(keys, static k => !IsTrait(k));
            var lists = new List<string>?[listKeys.Length];
            var selectors = new (string Key, List<string>? Items)[listKeys.Length];
            for (var i = 0; i < listKeys.Length; i++)
            {
                lists[i] = listKeys[i] switch
                {
                    "layers" => StringList(fields, "layers", where),
                    "assemblies" => PatternList(fields, "assemblies", where, Glob.Flat),
                    "members" => PatternList(fields, "members", where, MemberPattern),
                    _ => PatternList(fields, listKeys[i], where, Glob.Dotted),
                };
                selectors[i] = (listKeys[i], lists[i]);
            }

            var traits = ReadTraits(fields, where);
            if (fields.TryGetValue("layers", out var layersField) && OtherSelectorKeys.Any(fields.ContainsKey))
            {
                var others = Array.FindAll(keys, static k => Array.IndexOf(OtherSelectorKeys, k) >= 0);
                Add(layersField.Key, $"a target in {ruleWhere} cannot mix 'layers' with {JoinOr(others)}");
            }
            else if (NoneSelected(fields, selectors) && !traits.AnySelected(fields))
            {
                Add(map, $"{where} needs at least one of {JoinList(keys)}");
            }

            return _problems.Count == before
                ? new TargetSelector
                {
                    Layers = ListFor(lists, listKeys, "layers"),
                    Namespaces = ListFor(lists, listKeys, "namespaces"),
                    Assemblies = ListFor(lists, listKeys, "assemblies"),
                    Types = ListFor(lists, listKeys, "types"),
                    BaseTypes = ListFor(lists, listKeys, "baseTypes"),
                    Interfaces = ListFor(lists, listKeys, "interfaces"),
                    Attributes = ListFor(lists, listKeys, "attributes"),
                    Members = ListFor(lists, listKeys, "members"),
                    Names = traits.Names ?? [],
                    Kinds = traits.Kinds ?? [],
                    Accessibility = traits.Accessibility ?? [],
                    AssignableTo = traits.AssignableTo ?? [],
                    Sealed = traits.Sealed,
                    Abstract = traits.Abstract,
                    Static = traits.Static,
                    Nested = traits.Nested,
                }
                : null;
        }

        // The trait keys present in a mapping whose key set allows them; absent keys stay null.
        private Traits ReadTraits(Dictionary<string, Field> fields, string where) => new(
            PatternList(fields, "names", where, NamePattern),
            ReadKinds(fields, where),
            ReadAccessibilityList(fields, where),
            PatternList(fields, "assignableTo", where, Glob.Dotted),
            OptionalBool(fields, "sealed", where),
            OptionalBool(fields, "abstract", where),
            OptionalBool(fields, "static", where),
            OptionalBool(fields, "nested", where));

        private List<CSharpTypeKind>? ReadKinds(Dictionary<string, Field> fields, string where)
        {
            var values = StringList(fields, "kinds", where);
            if (values is null)
            {
                return null;
            }

            var sequence = (YamlSequenceNode)fields["kinds"].Value;
            var kinds = new List<CSharpTypeKind>(values.Count);
            for (var i = 0; i < values.Count; i++)
            {
                var at = Array.FindIndex(SelectableTypeKinds, entry => string.Equals(entry.Name, values[i], StringComparison.Ordinal));
                if (at < 0)
                {
                    Add(sequence.Children[i], $"kind '{PackText.Show(values[i])}' in {where} must be one of {KindsNames}");
                    continue;
                }

                kinds.Add(SelectableTypeKinds[at].Kind);
            }

            return kinds.Count == values.Count ? kinds : null;
        }

        private List<string>? ReadAccessibilityList(Dictionary<string, Field> fields, string where)
        {
            var values = StringList(fields, "accessibility", where);
            if (values is null)
            {
                return null;
            }

            var sequence = (YamlSequenceNode)fields["accessibility"].Value;
            var valid = true;
            for (var i = 0; i < values.Count; i++)
            {
                if (Array.IndexOf(AccessibilityKeywords, values[i]) < 0)
                {
                    Add(sequence.Children[i], $"accessibility '{PackText.Show(values[i])}' in {where} must be one of {AccessibilityNames}");
                    valid = false;
                }
            }

            return valid ? values : null;
        }

        private static List<string> ListFor(List<string>?[] lists, string[] keys, string key)
        {
            var at = Array.IndexOf(keys, key);
            return at < 0 ? [] : lists[at] ?? [];
        }

        private List<RuleException> ReadExceptions(Dictionary<string, Field> ruleFields, string ruleWhere, RuleKind kind)
        {
            var exceptions = new List<RuleException>();
            var items = Items(ruleFields, "exceptions", ruleWhere);
            for (var i = 0; i < items.Count && Visit(items[i]); i++)
            {
                if (items[i] is not YamlMappingNode map)
                {
                    Add(items[i], $"'exceptions[{i}]' in {ruleWhere} must be a mapping");
                    continue;
                }

                var where = $"exceptions[{i}] of {ruleWhere}";
                var before = _problems.Count;
                var fields = Fields(map, ExceptionKeys, where);
                var namespaces = PatternList(fields, "namespaces", where, Glob.Dotted);
                var assemblies = PatternList(fields, "assemblies", where, Glob.Flat);
                var types = PatternList(fields, "types", where, Glob.Dotted);
                var traits = ReadTraits(fields, where);
                var because = RequiredString(fields, "because", where, map);
                var misplaced = RejectMisplaced(fields, where, kind);
                if (!misplaced && NoneSelected(fields, [("namespaces", namespaces), ("assemblies", assemblies), ("types", types)])
                    && !traits.AnySelected(fields))
                {
                    var allowed = ExceptionKeyRules.Allowed(kind);
                    Add(map, allowed.Length == 1 ? $"{where} needs '{allowed[0]}'" : $"{where} needs at least one of {JoinList(allowed)}");
                }

                if (_problems.Count == before && because is not null)
                {
                    exceptions.Add(new RuleException
                    {
                        Namespaces = namespaces ?? [],
                        Assemblies = assemblies ?? [],
                        Types = types ?? [],
                        Names = traits.Names ?? [],
                        Kinds = traits.Kinds ?? [],
                        Accessibility = traits.Accessibility ?? [],
                        AssignableTo = traits.AssignableTo ?? [],
                        Sealed = traits.Sealed,
                        Abstract = traits.Abstract,
                        Static = traits.Static,
                        Nested = traits.Nested,
                        Because = because,
                    });
                }
            }

            return exceptions;
        }

        // The keys the rule kind does not take are reported at the key itself.
        private bool RejectMisplaced(Dictionary<string, Field> fields, string where, RuleKind kind)
        {
            var found = false;
            foreach (var (key, field) in fields)
            {
                if (ExceptionKeyRules.Rejection(kind, key) is { } reason)
                {
                    Add(field.Key, $"'{key}' is not accepted in {where}; {reason}");
                    found = true;
                }
            }

            return found;
        }

        private static string JoinList(string[] keys) => string.Join(", ", keys.Select(static k => $"'{k}'"));

        private static string JoinOr(IReadOnlyList<string> keys) =>
            keys.Count == 1
                ? $"'{keys[0]}'"
                : $"{string.Join(", ", keys.Take(keys.Count - 1).Select(static k => $"'{k}'"))} or '{keys[^1]}'";

        private void ReadRole(YamlNode node, int index, List<ComponentRoleDefinition> roles)
        {
            if (node is not YamlMappingNode map)
            {
                Add(node, $"'componentRoles[{index}]' in the pack must be a mapping");
                return;
            }

            var where = PeekString(map, "name") is { } known ? $"role '{PackText.Show(known)}'" : $"componentRoles[{index}]";
            var before = _problems.Count;
            var fields = Fields(map, RoleKeys, where);
            var name = RequiredString(fields, "name", where, map);
            var layer = RequiredString(fields, "layer", where, map);
            var typeKind = ReadTypeKind(fields, where);
            var requiresInterface = OptionalBool(fields, "requiresInterface", where);
            var interfaceRole = OptionalString(fields, "interfaceRole", where);
            var accessibility = OptionalString(fields, "accessibility", where);
            var description = OptionalString(fields, "description", where);
            var alternativeNames = StringList(fields, "alternativeNames", where);
            var allowEmpty = OptionalBool(fields, "allowEmpty", where);

            if (_problems.Count == before && name is not null && layer is not null)
            {
                var defaults = new ComponentRoleDefinition { Name = name, Layer = layer };
                roles.Add(new ComponentRoleDefinition
                {
                    Name = name,
                    Layer = layer,
                    TypeKind = typeKind ?? defaults.TypeKind,
                    RequiresInterface = requiresInterface ?? false,
                    InterfaceRole = interfaceRole,
                    Accessibility = accessibility ?? defaults.Accessibility,
                    Description = description ?? "",
                    AlternativeNames = alternativeNames ?? [],
                    AllowEmpty = allowEmpty ?? false,
                });
            }
        }

        private CSharpTypeKind? ReadTypeKind(Dictionary<string, Field> fields, string where, string key = "typeKind")
        {
            var value = OptionalString(fields, key, where);
            if (value is null)
            {
                return null;
            }

            foreach (var kind in BuildableTypeKinds)
            {
                if (string.Equals(kind.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    return kind;
                }
            }

            Add(fields[key].Key, $"{key} '{PackText.Show(value)}' in {where} must be one of {TypeKindNames}");
            return null;
        }

        private NamingConventions ReadNaming(Dictionary<string, Field> packFields, string packWhere)
        {
            var defaults = new NamingConventions();
            if (!packFields.TryGetValue("namingConventions", out var field))
            {
                return defaults;
            }

            if (field.Value is not YamlMappingNode map)
            {
                Add(field.Key, $"'namingConventions' in {packWhere} must be a mapping");
                return defaults;
            }

            const string where = "the namingConventions";
            var fields = Fields(map, NamingKeys, where);
            return new NamingConventions
            {
                Namespace = OptionalString(fields, "namespace", where) ?? defaults.Namespace,
                FilePath = OptionalString(fields, "filePath", where) ?? defaults.FilePath,
            };
        }

        // The known keys of a mapping. Reports unknown keys, with a suggestion, and the keys the parser saw twice.
        private Dictionary<string, Field> Fields(YamlMappingNode mapping, string[] allowed, string where, bool isPack = false)
        {
            var fields = new Dictionary<string, Field>(StringComparer.Ordinal);
            foreach (var (keyNode, value) in mapping.Children)
            {
                if (!Visit(keyNode))
                {
                    return fields;
                }

                if (keyNode is not YamlScalarNode { Value: { } key } scalar)
                {
                    Add(keyNode, $"a key in {where} must be a string");
                    continue;
                }

                if (Array.IndexOf(allowed, key) >= 0)
                {
                    fields.TryAdd(key, new Field(scalar, value));
                }
                else if (string.Equals(key, "members", StringComparison.Ordinal))
                {
                    Add(scalar, $"'members' is not accepted in {where}; it names APIs and only the 'to' of a 'forbid' or 'allowOnly' rule takes it");
                }
                else if (isPack && string.Equals(key, CodeFeatureRulesKey, StringComparison.Ordinal))
                {
                    Add(scalar, CodeFeatureRulesMessage);
                }
                else
                {
                    var suggestion = KeySuggestion.Closest(key, allowed);
                    var shown = PackText.Show(key);
                    Add(scalar, suggestion is null
                        ? $"unknown key '{shown}' in {where}"
                        : $"unknown key '{shown}' in {where}; did you mean '{suggestion}'?");
                }
            }

            // Removed once reported, so a mapping reached again through an alias does not report them twice.
            if (duplicates.Remove(DuplicateKeyFilter.Position(mapping.Start), out var seen))
            {
                foreach (ref readonly var duplicate in CollectionsMarshal.AsSpan(seen))
                {
                    _problems.Add(new PackProblem(duplicate.Line, duplicate.Column, $"duplicate key '{PackText.Show(duplicate.Key)}' in {where}"));
                }
            }

            return fields;
        }

        // Repeats inside a mapping the walk never reached, such as one under an unknown key: never silently dropped.
        private void ReportUnvisitedDuplicates()
        {
            if (_exhausted)
            {
                return;
            }

            foreach (var seen in duplicates.Values)
            {
                foreach (ref readonly var duplicate in CollectionsMarshal.AsSpan(seen))
                {
                    _problems.Add(new PackProblem(duplicate.Line, duplicate.Column, $"duplicate key '{PackText.Show(duplicate.Key)}' in the pack"));
                }
            }

            duplicates.Clear();
        }

        private string? RequiredString(Dictionary<string, Field> fields, string key, string where, YamlMappingNode parent)
        {
            if (!fields.TryGetValue(key, out var field))
            {
                Add(parent, $"{where} has no '{key}'");
                return null;
            }

            var value = AsString(in field, where);
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                Add(field.Key, $"{where} has no '{key}'");
                return null;
            }

            return value;
        }

        private string? OptionalString(Dictionary<string, Field> fields, string key, string where) =>
            fields.TryGetValue(key, out var field) ? AsString(in field, where) : null;

        private string? AsString(in Field field, string where)
        {
            if (field.Value is YamlScalarNode scalar && !IsNull(scalar))
            {
                return scalar.Value;
            }

            Add(field.Key, $"'{field.Name}' in {where} must be a string");
            return null;
        }

        private bool? OptionalBool(Dictionary<string, Field> fields, string key, string where)
        {
            if (!fields.TryGetValue(key, out var field))
            {
                return null;
            }

            if (field.Value is YamlScalarNode { Style: ScalarStyle.Plain, Value: { } value })
            {
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            Add(field.Key, $"'{key}' in {where} must be true or false");
            return null;
        }

        private List<string>? StringList(Dictionary<string, Field> fields, string key, string where)
        {
            if (!fields.TryGetValue(key, out var field))
            {
                return null;
            }

            if (field.Value is YamlSequenceNode sequence)
            {
                var values = new List<string>(sequence.Children.Count);
                foreach (var item in sequence.Children)
                {
                    if (!Visit(item))
                    {
                        return null;
                    }

                    if (item is not YamlScalarNode scalar || IsNull(scalar) || scalar.Value is null)
                    {
                        values = null;
                        break;
                    }

                    values.Add(scalar.Value);
                }

                if (values is not null)
                {
                    return values;
                }
            }

            Add(field.Key, $"'{key}' in {where} must be a list of strings");
            return null;
        }

        // A list of strings whose every entry compiles as a glob; an invalid pattern is reported at the entry itself.
        private List<string>? PatternList(Dictionary<string, Field> fields, string key, string where, Func<string, Result<Glob, string>> compile)
        {
            var values = StringList(fields, key, where);
            if (values is null)
            {
                return null;
            }

            var sequence = (YamlSequenceNode)fields[key].Value;
            for (var i = 0; i < values.Count; i++)
            {
                var glob = compile(values[i]);
                if (!glob.IsSuccess)
                {
                    // The glob's reason quotes the pattern too; it is shown the same way as the pattern itself.
                    var shown = PackText.Show(values[i]);
                    var reason = glob.Error.Replace($"'{values[i]}'", $"'{shown}'", StringComparison.Ordinal);
                    Add(sequence.Children[i], $"'{shown}' in {where} is not a valid pattern: {PackText.Escape(reason)}");
                }
            }

            return values;
        }

        // A name is the type's innermost simple name, so a dot could never match.
        private static Result<Glob, string> NamePattern(string pattern) =>
            pattern.Contains('.', StringComparison.Ordinal)
                ? Result<Glob, string>.Failure("a name pattern matches a type's simple name, so it cannot contain '.'")
                : Glob.Flat(pattern);

        // A member name always has a type segment before the member, so a one-segment pattern could never match. A pattern
        // that starts with '**' can still span both segments. The reason does not quote the pattern: the problem line does.
        private static Result<Glob, string> MemberPattern(string pattern) =>
            !pattern.Contains('.', StringComparison.Ordinal) && !pattern.StartsWith("**", StringComparison.Ordinal)
                ? Result<Glob, string>.Failure("a member pattern needs a type and a member, as 'Namespace.Type.Member'; start it with '**.' to match the member in any type")
                : Glob.Dotted(pattern);

        // The entries of a list-valued key; an absent key is an empty list.
        private IList<YamlNode> Items(Dictionary<string, Field> fields, string key, string where)
        {
            if (!fields.TryGetValue(key, out var field))
            {
                return [];
            }

            if (field.Value is YamlSequenceNode sequence)
            {
                return sequence.Children;
            }

            Add(field.Key, $"'{key}' in {where} must be a list");
            return [];
        }

        // Counts one visit. Once the budget is spent it reports that once, at this node, and the walk stops adding problems.
        private bool Visit(YamlNode node)
        {
            if (_exhausted)
            {
                return false;
            }

            if (++_visited <= MaxEntries)
            {
                return true;
            }

            Add(node, $"the pack expands to more than {MaxEntries} entries");
            _exhausted = true;
            return false;
        }

        // Nothing is added once the budget is spent: what is left of the walk only sees a partial pack.
        private void Add(YamlNode node, string message)
        {
            if (_exhausted)
            {
                return;
            }

            var (line, column) = DuplicateKeyFilter.Position(node.Start);
            _problems.Add(new PackProblem(line, column, message));
        }

        private static Field? FindKey(YamlMappingNode mapping, string key)
        {
            foreach (var (keyNode, value) in mapping.Children)
            {
                if (keyNode is YamlScalarNode scalar && string.Equals(scalar.Value, key, StringComparison.Ordinal))
                {
                    return new Field(scalar, value);
                }
            }

            return null;
        }

        // The entry's own name or id, read before its keys are checked, to say where a problem is.
        private static string? PeekString(YamlMappingNode mapping, string key) =>
            FindKey(mapping, key) is { Value: YamlScalarNode scalar } && !IsNull(scalar) && !string.IsNullOrWhiteSpace(scalar.Value)
                ? scalar.Value
                : null;

        private static YamlNode Later(YamlNode a, YamlNode b) =>
            a.Start.Line > b.Start.Line || (a.Start.Line == b.Start.Line && a.Start.Column > b.Start.Column) ? a : b;
    }
}
