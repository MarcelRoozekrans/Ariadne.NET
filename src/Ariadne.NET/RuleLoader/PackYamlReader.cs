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
        ["id", "because", "description", "forbid", "allowOnly", "exceptions", "locked"];

    private static readonly string[] AmendmentKeys = ["id", "exceptions"];

    private static readonly string[] KindKeys = ["from", "to"];

    private static readonly string[] TargetKeys = ["layers", "namespaces", "assemblies", "types"];

    private static readonly string[] ExceptionKeys = ["namespaces", "assemblies", "types", "because"];

    private static readonly string[] RoleKeys =
    [
        "name", "layer", "typeKind", "requiresInterface", "interfaceRole", "accessibility", "description", "alternativeNames",
    ];

    private static readonly string[] NamingKeys = ["namespace", "filePath"];

    private static readonly string TypeKindNames = string.Join(", ", Enum.GetNames<CSharpTypeKind>());

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
            var (line, column) = Position(ex.Start);
            return Result<RulePack, IReadOnlyList<PackProblem>>.Failure([new PackProblem(line, column, ex.Message)]);
        }

        return new Walk(parser.Duplicates).Read(stream);
    }

    private static (int Line, int Column) Position(Mark mark) => (checked((int)mark.Line), checked((int)mark.Column));

    private static bool IsNull(YamlScalarNode scalar) =>
        scalar.Style == ScalarStyle.Plain && scalar.Value is null or "" or "~" or "null" or "Null" or "NULL";

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

    private readonly record struct Field(YamlScalarNode Key, YamlNode Value)
    {
        public string Name => Key.Value ?? "";
    }

    private readonly record struct DuplicateKey(string Key, int Line, int Column);

    /// <summary>One read of one file: the walk over the representation model and the problems it found.</summary>
    private sealed class Walk(Dictionary<(int Line, int Column), List<DuplicateKey>> duplicates)
    {
        private readonly List<PackProblem> _problems = [];
        private bool _hasExtends;

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

            PackProblem[] ordered = [.. _problems.OrderBy(p => p.Line).ThenBy(p => p.Column)];
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
            for (var i = 0; i < items.Count; i++)
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
            for (var i = 0; i < items.Count; i++)
            {
                ReadRule(items[i], i, rules, amendments);
            }

            return (rules, amendments);
        }

        private List<ComponentRoleDefinition> ReadRoles(Dictionary<string, Field> packFields, string packWhere)
        {
            var roles = new List<ComponentRoleDefinition>();
            var items = Items(packFields, "componentRoles", packWhere);
            for (var i = 0; i < items.Count; i++)
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

            var where = PeekString(map, "name") is { } known ? $"layer '{known}'" : $"layers[{index}]";
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

            var where = PeekString(map, "id") is { } known ? $"rule '{known}'" : $"rules[{index}]";
            var before = _problems.Count;
            var fields = Fields(map, RuleKeys, where);
            var id = RequiredString(fields, "id", where, map);
            var exceptions = ReadExceptions(fields, where);

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

            var hasForbid = fields.TryGetValue("forbid", out var forbid);
            var hasAllowOnly = fields.TryGetValue("allowOnly", out var allowOnly);
            (TargetSelector From, TargetSelector To)? targets = null;
            if (hasForbid == hasAllowOnly)
            {
                YamlNode at = !hasForbid ? map : Later(forbid.Key, allowOnly.Key);
                Add(at, $"{where} must have exactly one of 'forbid' or 'allowOnly'");
            }
            else
            {
                targets = hasForbid ? ReadKind(in forbid, where) : ReadKind(in allowOnly, where);
            }

            if (_problems.Count == before && id is not null && because is not null && targets is { } pair)
            {
                rules.Add(new RuleDefinition
                {
                    Id = id,
                    Because = because,
                    Description = description ?? "",
                    Kind = hasForbid ? RuleKind.Forbid : RuleKind.AllowOnly,
                    From = pair.From,
                    To = pair.To,
                    Exceptions = exceptions,
                    Locked = locked ?? false,
                });
            }
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
            var from = ReadTarget(fields, "from", where, ruleWhere, map);
            var to = ReadTarget(fields, "to", where, ruleWhere, map);
            return from is not null && to is not null ? (from, to) : null;
        }

        private TargetSelector? ReadTarget(Dictionary<string, Field> kindFields, string key, string kindWhere, string ruleWhere, YamlMappingNode parent)
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

            var where = $"the {key} of {ruleWhere}";
            var before = _problems.Count;
            var fields = Fields(map, TargetKeys, where);
            var layers = StringList(fields, "layers", where);
            var namespaces = PatternList(fields, "namespaces", where, Glob.Dotted);
            var assemblies = PatternList(fields, "assemblies", where, Glob.Flat);
            var types = PatternList(fields, "types", where, Glob.Dotted);

            if (fields.TryGetValue("layers", out var layersField)
                && (fields.ContainsKey("namespaces") || fields.ContainsKey("assemblies") || fields.ContainsKey("types")))
            {
                Add(layersField.Key, $"a target in {ruleWhere} cannot mix 'layers' with 'namespaces', 'assemblies' or 'types'");
            }
            else if (NoneSelected(fields, [("layers", layers), ("namespaces", namespaces), ("assemblies", assemblies), ("types", types)]))
            {
                Add(map, $"{where} needs at least one of 'layers', 'namespaces', 'assemblies', 'types'");
            }

            return _problems.Count == before
                ? new TargetSelector { Layers = layers ?? [], Namespaces = namespaces ?? [], Assemblies = assemblies ?? [], Types = types ?? [] }
                : null;
        }

        private List<RuleException> ReadExceptions(Dictionary<string, Field> ruleFields, string ruleWhere)
        {
            var exceptions = new List<RuleException>();
            var items = Items(ruleFields, "exceptions", ruleWhere);
            for (var i = 0; i < items.Count; i++)
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
                var because = RequiredString(fields, "because", where, map);
                if (NoneSelected(fields, [("namespaces", namespaces), ("assemblies", assemblies), ("types", types)]))
                {
                    Add(map, $"{where} needs at least one of 'namespaces', 'assemblies', 'types'");
                }

                if (_problems.Count == before && because is not null)
                {
                    exceptions.Add(new RuleException
                    {
                        Namespaces = namespaces ?? [],
                        Assemblies = assemblies ?? [],
                        Types = types ?? [],
                        Because = because,
                    });
                }
            }

            return exceptions;
        }

        private void ReadRole(YamlNode node, int index, List<ComponentRoleDefinition> roles)
        {
            if (node is not YamlMappingNode map)
            {
                Add(node, $"'componentRoles[{index}]' in the pack must be a mapping");
                return;
            }

            var where = PeekString(map, "name") is { } known ? $"role '{known}'" : $"componentRoles[{index}]";
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
                });
            }
        }

        private CSharpTypeKind? ReadTypeKind(Dictionary<string, Field> fields, string where)
        {
            var value = OptionalString(fields, "typeKind", where);
            if (value is null)
            {
                return null;
            }

            foreach (var kind in Enum.GetValues<CSharpTypeKind>())
            {
                if (string.Equals(kind.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    return kind;
                }
            }

            Add(fields["typeKind"].Key, $"typeKind '{value}' in {where} must be one of {TypeKindNames}");
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
                if (keyNode is not YamlScalarNode { Value: { } key } scalar)
                {
                    Add(keyNode, $"unknown key '{keyNode}' in {where}");
                    continue;
                }

                if (Array.IndexOf(allowed, key) >= 0)
                {
                    fields.TryAdd(key, new Field(scalar, value));
                }
                else if (isPack && string.Equals(key, CodeFeatureRulesKey, StringComparison.Ordinal))
                {
                    Add(scalar, CodeFeatureRulesMessage);
                }
                else
                {
                    var suggestion = KeySuggestion.Closest(key, allowed);
                    Add(scalar, suggestion is null
                        ? $"unknown key '{key}' in {where}"
                        : $"unknown key '{key}' in {where}; did you mean '{suggestion}'?");
                }
            }

            // Removed once reported, so a mapping reached again through an alias does not report them twice.
            if (duplicates.Remove(Position(mapping.Start), out var seen))
            {
                foreach (ref readonly var duplicate in CollectionsMarshal.AsSpan(seen))
                {
                    _problems.Add(new PackProblem(duplicate.Line, duplicate.Column, $"duplicate key '{duplicate.Key}' in {where}"));
                }
            }

            return fields;
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
                    Add(sequence.Children[i], $"'{values[i]}' in {where} is not a valid pattern: {glob.Error}");
                }
            }

            return values;
        }

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

        private void Add(YamlNode node, string message)
        {
            var (line, column) = Position(node.Start);
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

    /// <summary>
    /// Passes parser events through, but drops the second and later occurrence of a scalar key in a mapping together
    /// with its value, recording it by the mapping's start. The representation model would otherwise stop at the first
    /// duplicate with an exception and no other problem would be reported.
    /// </summary>
    private sealed class DuplicateKeyFilter(IParser inner) : IParser
    {
        private readonly Stack<Frame> _frames = new();

        public Dictionary<(int Line, int Column), List<DuplicateKey>> Duplicates { get; } = [];

        public ParsingEvent? Current => inner.Current;

        public bool MoveNext()
        {
            while (inner.MoveNext())
            {
                var current = inner.Current;
                if (current is Scalar key
                    && _frames.TryPeek(out var frame)
                    && frame.IsMapping
                    && frame.ExpectKey
                    && !frame.Keys.Add(key.Value))
                {
                    Record(frame.Start, key);
                    SkipNode();
                    continue;
                }

                Track(current);
                return true;
            }

            return false;
        }

        private void Record(Mark mappingStart, Scalar key)
        {
            var at = Position(mappingStart);
            if (!Duplicates.TryGetValue(at, out var list))
            {
                list = [];
                Duplicates[at] = list;
            }

            var (line, column) = Position(key.Start);
            list.Add(new DuplicateKey(key.Value, line, column));
        }

        // Consumes the next node, a scalar, an alias or a whole collection.
        private void SkipNode()
        {
            var depth = 0;
            while (inner.MoveNext())
            {
                switch (inner.Current)
                {
                    case MappingStart or SequenceStart:
                        depth++;
                        break;
                    case MappingEnd or SequenceEnd:
                        depth--;
                        break;
                }

                if (depth == 0)
                {
                    return;
                }
            }
        }

        private void Track(ParsingEvent? current)
        {
            switch (current)
            {
                case DocumentStart:
                    _frames.Clear();
                    break;
                case MappingStart mapping:
                    _frames.Push(new Frame(isMapping: true, mapping.Start));
                    break;
                case SequenceStart sequence:
                    _frames.Push(new Frame(isMapping: false, sequence.Start));
                    break;
                case MappingEnd or SequenceEnd:
                    _frames.Pop();
                    Completed();
                    break;
                case Scalar or AnchorAlias:
                    Completed();
                    break;
            }
        }

        // A node finished: in a mapping, a key is followed by its value and a value by the next key.
        private void Completed()
        {
            if (_frames.TryPeek(out var frame) && frame.IsMapping)
            {
                frame.ExpectKey = !frame.ExpectKey;
            }
        }

        private sealed class Frame(bool isMapping, Mark start)
        {
            public bool IsMapping { get; } = isMapping;

            public Mark Start { get; } = start;

            public bool ExpectKey { get; set; } = true;

            public HashSet<string> Keys { get; } = new(StringComparer.Ordinal);
        }
    }
}
